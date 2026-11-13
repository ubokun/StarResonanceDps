// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 通知タブの VOICEVOX のスタイルの選択肢1つ(選んでいる話者のスタイル1つ)。
///
/// <para>
/// <see cref="IsMissing"/> は、保存してあるスタイルが、エンジンの一覧の話者のスタイルに無いことを表す。
/// 文言は作り直しで差し替える(言語が変わったら <c>SettingsViewModel</c> が作り直す)。
/// </para>
/// </summary>
public sealed class VoicevoxStyleOption
{
    public VoicevoxStyleOption(int styleId, string styleName, bool isMissing, string displayName)
    {
        StyleId = styleId;
        StyleName = styleName;
        IsMissing = isMissing;
        DisplayName = displayName;
    }

    public int StyleId { get; }

    public string StyleName { get; }

    public bool IsMissing { get; }

    public string DisplayName { get; }
}
