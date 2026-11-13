// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 集計設定「履歴の最大保持数」の選択肢1つ。
///
/// <para>
/// <see cref="Count"/> は保存値そのもの(<c>AppConfig.DatabaseMaxEncounterCount</c>)で、
/// <b>0 が無限。</b>ComboBox は <c>SelectedValue</c> でこれを直接束縛するので、
/// 表示の並び順と保存値の対応表を別に持たなくてよい。
/// </para>
///
/// <para>
/// <b>文言は作り直しで差し替える。</b>言語が変わると <see cref="DisplayName"/> も変わるが、
/// 通知を持たせずに <c>SettingsViewModel.RebuildRetentionPolicyOptions</c> が
/// コレクションごと作り直す(選択肢が4件しかないので、変更通知を足す価値がない)。
/// </para>
/// </summary>
public sealed class RetentionPolicyOption
{
    public RetentionPolicyOption(int count, string displayName)
    {
        Count = count;
        DisplayName = displayName;
    }

    public int Count { get; }

    public string DisplayName { get; }
}
