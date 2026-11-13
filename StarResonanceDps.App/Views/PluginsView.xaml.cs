// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Controls;

namespace StarResonanceDps.App.Views;

public partial class PluginsView : UserControl
{
    internal ScrollViewer ContentScrollViewer => PluginScrollViewer;

    public PluginsView()
    {
        InitializeComponent();
    }
}
