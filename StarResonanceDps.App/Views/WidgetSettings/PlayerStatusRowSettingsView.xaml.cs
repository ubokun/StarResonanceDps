// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Controls;

namespace StarResonanceDps.App.Views.WidgetSettings;

/// <summary>「ステータス」の一覧。行の並べ替えは一覧に付けた <c>Behaviors.RowReorderBehavior</c> が受け持つ。</summary>
public partial class PlayerStatusRowSettingsView : UserControl
{
    public PlayerStatusRowSettingsView()
    {
        InitializeComponent();
    }
}
