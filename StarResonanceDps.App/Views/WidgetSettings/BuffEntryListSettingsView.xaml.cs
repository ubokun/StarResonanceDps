// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Controls;

namespace StarResonanceDps.App.Views.WidgetSettings;

/// <summary>
/// バフ・デバフの行の一覧(バフ・デバフグループの「バフ・デバフ」、バフ・デバフカードの「ショートカット」)。
/// 行の並べ替えは一覧に付けた <c>Behaviors.RowReorderBehavior</c> が受け持つ。
/// </summary>
public partial class BuffEntryListSettingsView : UserControl
{
    public BuffEntryListSettingsView()
    {
        InitializeComponent();
    }
}
