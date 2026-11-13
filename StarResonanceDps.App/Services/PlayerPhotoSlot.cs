// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;

namespace StarResonanceDps.App.Services;

/// <summary>
/// 写真の欄1つ(顔・名刺)。写真の URL があれば取っている間も既定の絵を出し、取れたら写真に替える。
/// 取れなかったら既定の絵のまま(失敗は <see cref="PlayerPhotoLoader"/> がログに出す)。
/// ゲームの用意した絵(顔のプリセット)のファイルを渡されたら、既定の絵の代わりにそれを出す。画面のスレッドだけで使う。
/// </summary>
/// <param name="defaultResourceKey">既定の絵の資源の鍵。</param>
/// <param name="apply">出す絵を受け取る。</param>
public sealed class PlayerPhotoSlot(string defaultResourceKey, Action<ImageSource?> apply)
{
    private bool _hasShown;
    private string? _shownUrl;
    private string? _shownPresetPath;
    private CancellationTokenSource? _loading;

    /// <summary>
    /// いま出している写真(URL から取った絵)。既定の絵・プリセットの絵を出している間(取得中も含む)は null。
    /// 自分の写真の保存・コピーはこれだけを対象にする。
    /// </summary>
    public PlayerPhoto? Photo { get; private set; }

    /// <param name="url">写真の URL。無ければ既定の絵かプリセットの絵。</param>
    /// <param name="presetPath">ゲームの用意した絵のファイル。あれば既定の絵の代わりに出す。読めなければ既定の絵。</param>
    /// <param name="force">同じ URL でも取り直す。</param>
    public void Show(string? url, string? presetPath, long characterId, bool force)
    {
        if (!force && _hasShown && url == _shownUrl && presetPath == _shownPresetPath)
        {
            return;
        }

        _hasShown = true;
        _shownUrl = url;
        _shownPresetPath = presetPath;
        // 取り消しは取得中の要求が見ているので Dispose しない。
        _loading?.Cancel();
        _loading = null;

        Photo = null;
        var presetImage = presetPath is null ? null : PlayerPhotoLoader.LoadPresetImage(presetPath);
        apply(presetImage ?? Application.Current?.TryFindResource(defaultResourceKey) as ImageSource);
        if (url is null)
        {
            return;
        }

        var loading = new CancellationTokenSource();
        _loading = loading;
        _ = LoadAsync(url, characterId, loading);
    }

    private async Task LoadAsync(string url, long characterId, CancellationTokenSource loading)
    {
        var photo = await PlayerPhotoLoader.LoadAsync(url, characterId, loading.Token);
        if (photo is not null && !loading.IsCancellationRequested)
        {
            Photo = photo;
            apply(photo.Image);
        }
    }
}
