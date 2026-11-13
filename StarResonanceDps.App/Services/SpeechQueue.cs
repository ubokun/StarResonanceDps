// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Threading.Channels;
using Microsoft.Win32;
using StarResonanceDps.App.Config;
using Windows.Media.SpeechSynthesis;

namespace StarResonanceDps.App.Services;

/// <summary>順番待ちの1件。音量は通知した時点の、通知タブのその項目の通知音量。</summary>
internal abstract record NotificationAudioRequest(int Volume);

/// <summary>読み上げ1件。声は通知した時点の、通知タブのその項目の設定。</summary>
internal sealed record SpeechRequest(string Text, int SpeechVoiceIndex, int? VoicevoxStyleId, CultureInfo Culture, int Volume)
    : NotificationAudioRequest(Volume);

/// <summary>Windows の通知に添える音1件。音は Windows のサウンドの設定の「通知」。</summary>
internal sealed record NotificationSoundRequest(int Volume) : NotificationAudioRequest(Volume);

/// <summary>
/// 読み上げと Windows の通知の音の順番待ち。届いた順に1件ずつ鳴らし、重ねて鳴らさない。待ちの件数に上限は持たない。
/// 音は WAV に通知音量を掛けてから <see cref="SoundPlayer"/> で鳴らす(PlaySound は同じプロセスの前の音を止めるので、1件ずつにする)。
///
/// <para>
/// 通知音量は Windows のマスター音量のスライダーと同じ聞こえ方にする: 振幅の倍率 = (音量/100)^<see cref="VolumeCurveExponent"/>
/// (50% で約 -10dB、100% は元の大きさ)。VOICEVOX の読み上げだけは、音量の値を <see cref="VoicevoxVolumeScale"/> 倍してから掛ける
/// (50% が元の大きさ、100% で約 +10dB。VOICEVOX の声は元の大きさが小さいため)。上げるときは、ピークが 16bit の上限に届く倍率で止め、割らない。
/// 掛けられるのは 16bit の PCM だけ。ほかの形式は掛けずにそのまま鳴らし、そのことをログに残す。
/// Windows の音量ミキサー(セッションの音量)は使わない(Windows に値が残るため)。
/// </para>
///
/// <para>
/// 失敗(声が無い・VOICEVOX につながらない・話者を選んでいない・通知の音のファイルが読めない)は <see cref="NotificationFailureLog"/> に残してその1件を飛ばす。
/// 思いがけない例外も、その1件だけ捨ててログに残し、次の1件へ進む(順番待ちを止めない)。
/// </para>
/// </summary>
internal sealed class SpeechQueue : IDisposable
{
    private const double VolumeCurveExponent = 1.71;

    /// <summary>VOICEVOX の読み上げに掛けるときの音量の値の倍率。通知音量 100% は値 200 として曲線に通す。</summary>
    private const int VoicevoxVolumeScale = 2;

    // 既定値が Windows のサウンドの設定の「通知」の音のファイル。空なら利用者が「なし」を選んでいる。
    private const string NotificationSoundKeyPath = @"AppEvents\Schemes\Apps\.Default\Notification.Default\.Current";

    private readonly Channel<NotificationAudioRequest> _requests = Channel.CreateUnbounded<NotificationAudioRequest>(new UnboundedChannelOptions
    {
        SingleReader = true
    });

    private readonly CancellationTokenSource _stopping = new();
    private readonly object _playerSync = new();
    private SoundPlayer? _currentPlayer;

    public SpeechQueue()
    {
        _ = Task.Run(RunAsync);
    }

    public void Enqueue(NotificationAudioRequest request)
    {
        _requests.Writer.TryWrite(request);
    }

    public void Dispose()
    {
        _requests.Writer.TryComplete();
        _stopping.Cancel();
        lock (_playerSync)
        {
            _currentPlayer?.Stop();
        }
    }

    private async Task RunAsync()
    {
        try
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(_stopping.Token).ConfigureAwait(false))
            {
                try
                {
                    var wave = request switch
                    {
                        SpeechRequest speech => await SynthesizeAsync(speech).ConfigureAwait(false),
                        NotificationSoundRequest => LoadNotificationSound(),
                        _ => throw new InvalidOperationException($"Unknown notification audio request {request.GetType().Name}")
                    };

                    if (wave is not null)
                    {
                        var volume = request is SpeechRequest { SpeechVoiceIndex: AppConfigDefaults.SpeechVoiceVoicevoxIndex }
                            ? request.Volume * VoicevoxVolumeScale
                            : request.Volume;
                        Play(ApplyVolume(wave, volume));
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    NotificationFailureLog.Warning("speech-unexpected", ex, "Could not play a notification sound or read a notification aloud");
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
            // アプリの終了。待っていた読み上げは捨てる。
        }
    }

    private void Play(byte[] wave)
    {
        using var stream = new MemoryStream(wave);
        using var player = new SoundPlayer(stream);
        lock (_playerSync)
        {
            if (_stopping.IsCancellationRequested)
            {
                return;
            }

            _currentPlayer = player;
        }

        try
        {
            player.PlaySync();
        }
        finally
        {
            lock (_playerSync)
            {
                _currentPlayer = null;
            }
        }
    }

    /// <summary>Windows のサウンドの設定の「通知」の音を読む。「なし」なら null(鳴らさない)。読めなければログに残して null。</summary>
    private static byte[]? LoadNotificationSound()
    {
        string? path;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(NotificationSoundKeyPath);

            // 既定値は %SystemRoot% を含む REG_EXPAND_SZ のことがある(GetValue が展開する)。
            path = key?.GetValue(null) as string;
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            NotificationFailureLog.Warning(
                "notification-sound-registry",
                ex,
                "Could not read the Windows notification sound setting key={Key}. The sound was not played",
                NotificationSoundKeyPath);
            return null;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or SecurityException)
        {
            NotificationFailureLog.Warning(
                "notification-sound-file",
                ex,
                "Could not read the Windows notification sound path={Path}. The sound was not played",
                path);
            return null;
        }
    }

    /// <summary>
    /// 通知音量を WAV のサンプルに掛ける。値が 100 なら元のまま(VOICEVOX は倍率を掛けた後の値)。上げるときはピークが 16bit の上限に届く倍率で止める。
    /// 16bit の PCM でなければ掛けずに返し、ログに残す。
    /// </summary>
    private static byte[] ApplyVolume(byte[] wave, int volume)
    {
        if (volume == AppConfigDefaults.NotificationVolumeOriginal)
        {
            return wave;
        }

        if (!TryFindPcm16Data(wave, out var dataOffset, out var dataLength))
        {
            NotificationFailureLog.Warning(
                "notification-volume-format",
                null,
                "The notification volume works only on 16-bit PCM WAV. The sound was played without the volume");
            return wave;
        }

        var scaled = (byte[])wave.Clone();
        var samples = MemoryMarshal.Cast<byte, short>(scaled.AsSpan(dataOffset, dataLength - (dataLength % 2)));

        var gain = Math.Pow(volume / (double)AppConfigDefaults.NotificationVolumeOriginal, VolumeCurveExponent);
        if (gain > 1)
        {
            var peak = 0;
            foreach (var sample in samples)
            {
                peak = Math.Max(peak, Math.Abs((int)sample));
            }

            if (peak > 0)
            {
                gain = Math.Min(gain, short.MaxValue / (double)peak);
            }
        }

        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (short)Math.Clamp(Math.Round(samples[i] * gain, MidpointRounding.AwayFromZero), short.MinValue, short.MaxValue);
        }

        return scaled;
    }

    /// <summary>RIFF のチャンクをたどり、fmt が 16bit の PCM なら data の位置と長さを返す。</summary>
    private static bool TryFindPcm16Data(byte[] wave, out int dataOffset, out int dataLength)
    {
        dataOffset = 0;
        dataLength = 0;

        var span = wave.AsSpan();
        if (span.Length < 12 || !span[..4].SequenceEqual("RIFF"u8) || !span.Slice(8, 4).SequenceEqual("WAVE"u8))
        {
            return false;
        }

        var isPcm16 = false;
        long position = 12;
        while (position + 8 <= span.Length)
        {
            var header = span.Slice((int)position, 8);
            var chunkSize = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);
            var body = position + 8;
            var available = span.Length - body;

            if (header[..4].SequenceEqual("fmt "u8))
            {
                if (chunkSize < 16 || chunkSize > available)
                {
                    return false;
                }

                var format = span.Slice((int)body, 16);
                isPcm16 = BinaryPrimitives.ReadUInt16LittleEndian(format) == 1
                    && BinaryPrimitives.ReadUInt16LittleEndian(format[14..]) == 16;
            }
            else if (header[..4].SequenceEqual("data"u8))
            {
                if (!isPcm16)
                {
                    return false;
                }

                dataOffset = (int)body;
                dataLength = (int)Math.Min(chunkSize, available);
                return true;
            }

            // チャンクは偶数の長さに詰め物が入る。
            position = body + chunkSize + (chunkSize & 1);
        }

        return false;
    }

    private static async Task<byte[]?> SynthesizeAsync(SpeechRequest request)
    {
        if (request.SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceVoicevoxIndex)
        {
            return await SynthesizeWithVoicevoxAsync(request).ConfigureAwait(false);
        }

        if (!WindowsVoiceSelector.IsSupported)
        {
            NotificationFailureLog.Warning(
                "speech-windows-os",
                null,
                "The Windows voice needs Windows 10 or later. The notification was not read aloud");
            return null;
        }

        return await SynthesizeWithWindowsVoiceAsync(request).ConfigureAwait(false);
    }

    private static async Task<byte[]?> SynthesizeWithVoicevoxAsync(SpeechRequest request)
    {
        if (request.VoicevoxStyleId is not { } styleId)
        {
            NotificationFailureLog.Warning(
                "speech-voicevox-speaker",
                null,
                "No VOICEVOX speaker is selected. The notification was not read aloud");
            return null;
        }

        var result = await VoicevoxClient.SynthesizeAsync(request.Text, styleId).ConfigureAwait(false);
        if (result.Kind == VoicevoxResultKind.Success)
        {
            return result.Value;
        }

        NotificationFailureLog.Warning(
            "speech-voicevox",
            null,
            "Could not synthesize with VOICEVOX ({Detail}). The notification was not read aloud",
            result.Detail);
        return null;
    }

    [SupportedOSPlatform("windows10.0.10240")]
    private static async Task<byte[]?> SynthesizeWithWindowsVoiceAsync(SpeechRequest request)
    {
        var voice = WindowsVoiceSelector.FindVoice(request.Culture);
        if (voice is null)
        {
            NotificationFailureLog.Warning(
                "speech-windows-voice",
                null,
                "No Windows voice for {Culture}. The notification was not read aloud",
                request.Culture.Name);
            return null;
        }

        try
        {
            using var synthesizer = new SpeechSynthesizer { Voice = voice };
            using var stream = await synthesizer.SynthesizeTextToStreamAsync(request.Text);
            using var input = stream.AsStreamForRead();
            using var buffer = new MemoryStream();
            await input.CopyToAsync(buffer).ConfigureAwait(false);
            return buffer.ToArray();
        }
        catch (COMException ex)
        {
            NotificationFailureLog.Warning(
                "speech-windows",
                ex,
                "Could not synthesize with the Windows voice. The notification was not read aloud");
            return null;
        }
    }
}
