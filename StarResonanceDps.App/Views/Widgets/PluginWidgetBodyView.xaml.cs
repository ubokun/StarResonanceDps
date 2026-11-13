// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>プラグインのウィジェットの本体(スクロールしない)とフッターの置き場。余白は付けない。</summary>
public partial class PluginWidgetBodyView : UserControl, IPluginWidgetElementHost
{
    /// <param name="element">中身。空の窓は null で、後から <see cref="SetElement"/> で入れる。</param>
    public PluginWidgetBodyView(FrameworkElement? element)
    {
        InitializeComponent();
        ElementHost.Content = element;
    }

    public void SetElement(FrameworkElement element)
    {
        ElementHost.Content = element;
    }
}
