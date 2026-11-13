// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 集計設定「自動一時停止」の選択肢1つ。
///
/// <para>
/// <see cref="Seconds"/> は保存値そのもの(<c>AppConfig.CombatExitSeconds</c>)で、<b>0 は一時停止しない。</b>
/// ComboBox は <c>SelectedValue</c> でこれを直接束縛する。文言は言語が変わったら
/// <c>SettingsViewModel.RebuildCombatExitOptions</c> がコレクションごと作り直す(<see cref="RetentionPolicyOption"/> と同じ)。
/// </para>
/// </summary>
public sealed class CombatExitOption
{
    public CombatExitOption(int seconds, string displayName)
    {
        Seconds = seconds;
        DisplayName = displayName;
    }

    public int Seconds { get; }

    public string DisplayName { get; }
}
