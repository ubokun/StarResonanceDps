// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 通知タブの VOICEVOX の話者の選択肢1つ(話者1人)。スタイルは <see cref="VoicevoxStyleOption"/>。
///
/// <para>
/// <see cref="IsMissing"/> は、保存してある話者がエンジンの一覧に無いことを表す。
/// 空欄に見せると「未選択」と区別できないので、その話者を「(見つかりません)」付きで選択肢に残す。
/// 文言は作り直しで差し替える(言語が変わったら <c>SettingsViewModel</c> が作り直す)。
/// </para>
/// </summary>
public sealed class VoicevoxSpeakerOption
{
    public VoicevoxSpeakerOption(string speakerUuid, string speakerName, bool isMissing, string displayName)
    {
        SpeakerUuid = speakerUuid;
        SpeakerName = speakerName;
        IsMissing = isMissing;
        DisplayName = displayName;
    }

    public string SpeakerUuid { get; }

    public string SpeakerName { get; }

    public bool IsMissing { get; }

    public string DisplayName { get; }
}
