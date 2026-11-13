// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;

namespace StarResonanceDps.App.Views.Widgets;

/// <summary>プラグインのウィジェットの本体の置き場。空の窓は中身なしで作り、相手が決まったら後から入れる。</summary>
public interface IPluginWidgetElementHost
{
    void SetElement(FrameworkElement element);
}
