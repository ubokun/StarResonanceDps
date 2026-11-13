// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>VOICEVOX への問い合わせの結果の種類。</summary>
public enum VoicevoxResultKind
{
    Success,

    /// <summary>エンジンにつながらない(起動していない・入っていない)。</summary>
    ConnectFailed,

    /// <summary>つながったが、応答を読めない。</summary>
    BadResponse
}

/// <summary>VOICEVOX への問い合わせの結果。<see cref="Value"/> は成功のときだけ入る。</summary>
public sealed record VoicevoxResult<T>(VoicevoxResultKind Kind, T? Value)
{
    /// <summary>失敗の中身(例外の文や応答のコード)。ログを呼び出し側で書く問い合わせ(合成)だけが入れる。</summary>
    public string? Detail { get; init; }

    public static VoicevoxResult<T> Success(T value) => new(VoicevoxResultKind.Success, value);

    public static VoicevoxResult<T> Failure(VoicevoxResultKind kind) => new(kind, default);

    public static VoicevoxResult<T> Failure(VoicevoxResultKind kind, string detail) => new(kind, default) { Detail = detail };
}

/// <summary>VOICEVOX の話者の1つのスタイル。合成にはスタイルの番号を渡す。</summary>
public sealed record VoicevoxStyle(string SpeakerUuid, string SpeakerName, int StyleId, string StyleName);

/// <summary>
/// 利用者が入れた VOICEVOX のエンジン(ローカルの HTTP API)に問い合わせる。エンジンは同梱しない。
///
/// <para>
/// 接続先は、エディタが書く <c>%APPDATA%\voicevox\runtime-info.json</c> の最初のエンジンの URL。
/// ファイルが無ければ(古い版・エンジン単体の起動)エンジンの既定の <c>127.0.0.1:50021</c>。
/// 失敗は「つながらない」と「応答を読めない」に分けて返し、英語の警告をログに残す(合成だけはログを呼び出し側が書く)。
/// </para>
/// </summary>
public static class VoicevoxClient
{
    /// <summary>VOICEVOX の公式サイト(ダウンロードのページ)。設定画面のリンクと、つながらないときのメッセージで使う。</summary>
    public const string DownloadPageUrl = "https://voicevox.hiroshiba.jp/";

    private const string DefaultBaseUrl = "http://127.0.0.1:50021";

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(5) };

    /// <summary>スタイルの初期化だけに使う。初期化を待つ処理は無いので、時間では打ち切らない。</summary>
    private static readonly HttpClient InitializeClient = new() { Timeout = Timeout.InfiniteTimeSpan };

    /// <summary>エンジンにつながるか(<c>GET /version</c>)。</summary>
    public static async Task<VoicevoxResultKind> CheckConnectionAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetStringAsync("/version", cancellationToken).ConfigureAwait(false);
        return result.Kind;
    }

    /// <summary>
    /// 読み上げに使える話者のスタイルの一覧(<c>GET /speakers</c>)。並びはエンジンの順のまま。
    /// <c>/speakers</c> はエンジンが喋れるスタイルだけに絞った一覧(歌えるスタイルは <c>/singers</c>)なので、スタイルの種類では絞らない。
    /// </summary>
    public static async Task<VoicevoxResult<IReadOnlyList<VoicevoxStyle>>> GetStylesAsync(CancellationToken cancellationToken = default)
    {
        var response = await GetStringAsync("/speakers", cancellationToken).ConfigureAwait(false);
        if (response.Kind != VoicevoxResultKind.Success)
        {
            return VoicevoxResult<IReadOnlyList<VoicevoxStyle>>.Failure(response.Kind);
        }

        try
        {
            using var document = JsonDocument.Parse(response.Value!);
            var styles = new List<VoicevoxStyle>();
            foreach (var speaker in document.RootElement.EnumerateArray())
            {
                var speakerName = speaker.GetProperty("name").GetString() ?? string.Empty;
                var speakerUuid = speaker.GetProperty("speaker_uuid").GetString() ?? string.Empty;
                foreach (var style in speaker.GetProperty("styles").EnumerateArray())
                {
                    styles.Add(new VoicevoxStyle(
                        speakerUuid,
                        speakerName,
                        style.GetProperty("id").GetInt32(),
                        style.GetProperty("name").GetString() ?? string.Empty));
                }
            }

            return VoicevoxResult<IReadOnlyList<VoicevoxStyle>>.Success(styles);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            Log.Warning(ex, "Could not read the speaker list from VOICEVOX");
            return VoicevoxResult<IReadOnlyList<VoicevoxStyle>>.Failure(VoicevoxResultKind.BadResponse);
        }
    }

    /// <summary>話者の規約の本文(<c>GET /speaker_info</c> の policy、Markdown)。画像は埋め込ませない(resource_format=url)。</summary>
    public static async Task<VoicevoxResult<string>> GetPolicyAsync(string speakerUuid, CancellationToken cancellationToken = default)
    {
        var response = await GetStringAsync(
            $"/speaker_info?speaker_uuid={Uri.EscapeDataString(speakerUuid)}&resource_format=url",
            cancellationToken).ConfigureAwait(false);
        if (response.Kind != VoicevoxResultKind.Success)
        {
            return response;
        }

        try
        {
            using var document = JsonDocument.Parse(response.Value!);
            return VoicevoxResult<string>.Success(document.RootElement.GetProperty("policy").GetString() ?? string.Empty);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            Log.Warning(ex, "Could not read the policy of VOICEVOX speaker {SpeakerUuid}", speakerUuid);
            return VoicevoxResult<string>.Failure(VoicevoxResultKind.BadResponse);
        }
    }

    /// <summary>
    /// 文章を選んだスタイルで合成する(<c>POST /audio_query</c> → <c>POST /synthesis</c>)。返すのは WAV のバイト列。
    /// 読み上げ(通知)の途中で失敗が続いてもログが埋まらないよう、<b>ここではログを書かず</b>、失敗の中身を <see cref="VoicevoxResult{T}.Detail"/> に入れて返す。
    /// </summary>
    public static async Task<VoicevoxResult<byte[]>> SynthesizeAsync(string text, int styleId, CancellationToken cancellationToken = default)
    {
        var query = await PostAsync(
            Client,
            $"/audio_query?text={Uri.EscapeDataString(text)}&speaker={styleId}",
            null,
            cancellationToken).ConfigureAwait(false);
        if (query.Kind != VoicevoxResultKind.Success)
        {
            return query;
        }

        using var queryBody = new ByteArrayContent(query.Value!);
        queryBody.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return await PostAsync(Client, $"/synthesis?speaker={styleId}", queryBody, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// スタイルを初期化する(<c>POST /initialize_speaker</c>)。エンジンは初めて使うスタイルの合成に時間がかかり、
    /// 合成の打ち切りを越えると通知を読み上げられないので、通知より前に済ませておく。初期化済みならエンジンはやり直さない。
    /// 合成と同じく<b>ここではログを書かず</b>、失敗の中身を <see cref="VoicevoxResult{T}.Detail"/> に入れて返す。
    /// </summary>
    public static Task<VoicevoxResult<byte[]>> InitializeStyleAsync(int styleId, CancellationToken cancellationToken = default)
    {
        return PostAsync(InitializeClient, $"/initialize_speaker?speaker={styleId}&skip_reinit=true", null, cancellationToken);
    }

    private static async Task<VoicevoxResult<byte[]>> PostAsync(HttpClient client, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        var baseUrl = ResolveBaseUrl();
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(baseUrl + path, content, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            return VoicevoxResult<byte[]>.Failure(VoicevoxResultKind.ConnectFailed, $"Could not connect to {baseUrl}: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return VoicevoxResult<byte[]>.Failure(VoicevoxResultKind.ConnectFailed, $"{baseUrl} did not respond in time");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return VoicevoxResult<byte[]>.Failure(VoicevoxResultKind.BadResponse, $"{baseUrl} returned {(int)response.StatusCode}");
            }

            try
            {
                var body = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                return VoicevoxResult<byte[]>.Success(body);
            }
            catch (HttpRequestException ex)
            {
                return VoicevoxResult<byte[]>.Failure(VoicevoxResultKind.ConnectFailed, $"The connection to {baseUrl} was lost: {ex.Message}");
            }
        }
    }

    private static async Task<VoicevoxResult<string>> GetStringAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        var baseUrl = ResolveBaseUrl();
        HttpResponseMessage response;
        try
        {
            response = await Client.GetAsync(baseUrl + pathAndQuery, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            Log.Warning(ex, "Could not connect to VOICEVOX at {BaseUrl}", baseUrl);
            return VoicevoxResult<string>.Failure(VoicevoxResultKind.ConnectFailed);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            Log.Warning(ex, "VOICEVOX at {BaseUrl} did not respond in time", baseUrl);
            return VoicevoxResult<string>.Failure(VoicevoxResultKind.ConnectFailed);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("VOICEVOX returned {StatusCode} for {Path}", (int)response.StatusCode, pathAndQuery);
                return VoicevoxResult<string>.Failure(VoicevoxResultKind.BadResponse);
            }

            try
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return VoicevoxResult<string>.Success(body);
            }
            catch (HttpRequestException ex)
            {
                Log.Warning(ex, "The connection to VOICEVOX at {BaseUrl} was lost while reading {Path}", baseUrl, pathAndQuery);
                return VoicevoxResult<string>.Failure(VoicevoxResultKind.ConnectFailed);
            }
        }
    }

    private static string ResolveBaseUrl()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "voicevox",
            "runtime-info.json");
        if (!File.Exists(path))
        {
            return DefaultBaseUrl;
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.TryGetProperty("engineInfos", out var engines)
                && engines.ValueKind == JsonValueKind.Array
                && engines.GetArrayLength() > 0
                && engines[0].TryGetProperty("url", out var urlElement)
                && Uri.TryCreate(urlElement.GetString(), UriKind.Absolute, out var url)
                && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps))
            {
                return url.GetLeftPart(UriPartial.Authority);
            }

            Log.Warning("VOICEVOX runtime-info.json has no engine URL; using {BaseUrl}", DefaultBaseUrl);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            Log.Warning(ex, "Could not read VOICEVOX runtime-info.json; using {BaseUrl}", DefaultBaseUrl);
        }

        return DefaultBaseUrl;
    }
}
