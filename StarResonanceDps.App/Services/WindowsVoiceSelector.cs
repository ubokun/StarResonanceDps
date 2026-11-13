// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Serilog;
using Windows.Media.SpeechSynthesis;

namespace StarResonanceDps.App.Services;

/// <summary>
/// Windows の音声(WinRT の音声合成)から、表示言語に合う声を選ぶ。
/// Windows の既定の声がその言語なら既定の声、違えばその言語の声(地域まで同じものを先に)。
/// 無ければ null を返す。黙って別の言語の声にはしない(読ませると文と声の言語が食い違う)。
/// </summary>
public static class WindowsVoiceSelector
{
    /// <summary>
    /// WinRT の音声合成が使える Windows か。対応範囲は版を付けない形と同じに保っているので、呼ぶ前にここで確かめる。
    /// </summary>
    [SupportedOSPlatformGuard("windows10.0.10240")]
    public static bool IsSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240);

    public static VoiceInformation? FindVoice(CultureInfo culture)
    {
        if (!IsSupported)
        {
            return null;
        }

        try
        {
            var defaultVoice = SpeechSynthesizer.DefaultVoice;
            if (IsSameLanguage(defaultVoice, culture))
            {
                return defaultVoice;
            }

            VoiceInformation? sameLanguageVoice = null;
            foreach (var voice in SpeechSynthesizer.AllVoices)
            {
                if (string.Equals(voice.Language, culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return voice;
                }

                if (sameLanguageVoice is null && IsSameLanguage(voice, culture))
                {
                    sameLanguageVoice = voice;
                }
            }

            return sameLanguageVoice;
        }
        catch (COMException ex)
        {
            Log.Warning(ex, "Could not read the Windows voices");
            return null;
        }
    }

    [SupportedOSPlatform("windows10.0.10240")]
    private static bool IsSameLanguage(VoiceInformation? voice, CultureInfo culture)
    {
        if (voice is null)
        {
            return false;
        }

        var separator = voice.Language.IndexOf('-');
        var language = separator < 0 ? voice.Language : voice.Language[..separator];
        return string.Equals(language, culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase);
    }
}
