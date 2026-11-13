// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Net.Http;
using System.Windows.Media.Imaging;
using Serilog;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

/// <summary>取った写真。絵と、届いたままの中身。</summary>
/// <param name="Url">取った URL。保存するときの既定のファイル名と拡張子はここから決める。</param>
/// <param name="Data">届いたままの中身。保存はこれをそのまま書く(拡張子と中身を一致させるため)。</param>
public sealed record PlayerPhoto(string Url, BitmapSource Image, byte[] Data);

/// <summary>
/// プレイヤー情報の顔写真・名刺の写真を取る。URL は通信で届いたものをそのまま使う。
///
/// <para>
/// <b>ファイルには残さない。</b> URL を WPF にそのまま渡すとシステムのキャッシュに残り得るので、
/// メモリに読んでから絵にする。届いたままの中身も絵と一緒に返す(ユーザーが保存を選んだときに書く)。
/// </para>
/// </summary>
public static class PlayerPhotoLoader
{
    /// <summary>顔の既定の絵。写真を取れたときとゲームの用意したアイコンのとき以外はこれを出す。</summary>
    public const string DefaultAvatarResourceKey = "Icon.Avatar.head_photo_000";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>ファイルのパスごとのプリセットの絵。読めなかったファイルは null を控える。画面のスレッドで引く。</summary>
    private static readonly Dictionary<string, BitmapSource?> PresetImages = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>絵が見つからなかったプリセットの番号。ログの警告を番号ごとに1回にする。画面のスレッドで引く。</summary>
    private static readonly HashSet<int> MissingPresetIds = [];

    /// <summary>
    /// 顔の写真の URL。アップロードした写真の人(<c>avatarId</c> 0 か 1)で審査済みの写真があるときだけ。
    /// ゲームの用意したアイコンを選んだ人(それ以外の <c>avatarId</c>)は <see cref="GetAvatarPresetPath"/> の絵。
    /// </summary>
    public static string? GetAvatarPhotoUrl(SocialAvatarState? avatar)
    {
        return avatar is { AvatarId: 0 or 1, IsProfileReviewed: true } && !string.IsNullOrEmpty(avatar.ProfileUrl)
            ? avatar.ProfileUrl
            : null;
    }

    /// <summary>
    /// ゲームの用意したアイコンの絵のファイル。<c>avatarId</c> が 0・1 以外の人だけ。
    /// 表に番号が無い・ファイルが無ければ null(既定の絵を出す)で、ログに警告を番号ごとに1回出す。
    /// </summary>
    public static string? GetAvatarPresetPath(SocialAvatarState? avatar)
    {
        if (avatar is null or { AvatarId: 0 or 1 })
        {
            return null;
        }

        var path = CombatIconResolver.ResolveAvatarIcon(CombatDataCatalog.GetAvatarPresetImage(avatar.AvatarId));
        if (path is null && MissingPresetIds.Add(avatar.AvatarId))
        {
            Log.Warning("Avatar preset {AvatarId} has no image; the default avatar is shown instead", avatar.AvatarId);
        }

        return path;
    }

    /// <summary>
    /// プリセットの絵をファイルから読む。ファイルごとに1回だけ読んで凍らせ、使い回す。読めなければ null で、ログに残す。
    /// </summary>
    public static BitmapSource? LoadPresetImage(string path)
    {
        if (PresetImages.TryGetValue(path, out var cached))
        {
            return cached;
        }

        BitmapSource? image = null;
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            image = bitmap;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load avatar preset image {Path}; the default avatar is shown instead", path);
        }

        PresetImages[path] = image;
        return image;
    }

    /// <summary>名刺の半身の写真の URL。審査済みの写真があるときだけ(ゲームの名刺と同じ条件)。</summary>
    public static string? GetCardPhotoUrl(SocialAvatarState? avatar)
    {
        return avatar is { IsHalfBodyReviewed: true } && !string.IsNullOrEmpty(avatar.HalfBodyUrl)
            ? avatar.HalfBodyUrl
            : null;
    }

    /// <summary>
    /// 写真を取ってメモリの絵にする。https 以外と取得の失敗は null で、ログに警告を1行出す。取り消されたら null。
    /// </summary>
    /// <param name="characterId">ログに出す相手。URL はログに出さない。</param>
    public static async Task<PlayerPhoto?> LoadAsync(string url, long characterId, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            Log.Warning("Player photo URL is not https (character {CharacterId})", characterId);
            return null;
        }

        try
        {
            var bytes = await Client.GetByteArrayAsync(uri, cancellationToken).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            return new PlayerPhoto(url, image, bytes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load player photo (character {CharacterId})", characterId);
            return null;
        }
    }
}
