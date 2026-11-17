// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// バフ稼働率の1行。行は番号(料理・薬剤はまとまり)ごとに1つで、作り直さず値だけ入れ替える。
/// アイコンの項目の名前はバフリストの行(<see cref="PlayerBuffEntry"/>)と同じで、画面の作りも同じ形にしている。
/// </summary>
public sealed partial class BuffUptimeEntry : ObservableObject
{
    public BuffUptimeEntry(int buffId, BuffGroup group)
    {
        BuffId = buffId;
        Group = group;
        var icon = BuffIconDisplay.Resolve(group == BuffGroup.None
            ? CombatDataCatalog.GetBuffOwnIconName(buffId)
            : CombatDataCatalog.GetBuffGroupIconName(group));
        IconPath = icon.IconPath;
        IconMask = icon.IconMask;
        IsModuleEffectIcon = icon.IsModuleEffectIcon;
        IsFactorIcon = icon.IsFactorIcon;
        ShowsUnknownIcon = icon.ShowsUnknownIcon;
    }

    /// <summary>畳み先のバフの番号。料理・薬剤のまとまりは 0。</summary>
    public int BuffId { get; }

    public BuffGroup Group { get; }

    /// <summary>料理・薬剤のまとまりの行か。まとまりの行は優先表示・非表示の対象にならない(右クリックのメニューを出さない)。</summary>
    public bool IsGroup => Group != BuffGroup.None;

    public string? IconPath { get; }

    public ImageBrush? IconMask { get; }

    public bool IsModuleEffectIcon { get; }

    public bool IsFactorIcon { get; }

    public bool ShowsUnknownIcon { get; }

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>付いていた区間(戦闘の経過の秒、[始まり, 終わり] を交互)。中身が変わったときだけ差し替える。</summary>
    [ObservableProperty]
    private double[] _spans = [];

    [ObservableProperty]
    private string _rateText = string.Empty;
}
