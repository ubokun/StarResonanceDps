// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;
using StarResonanceDps.App.ViewModels.WidgetSettings;

namespace StarResonanceDps.App.Views.WidgetSettings;

public partial class BuffGaugeColorSettingsView : UserControl
{
    public BuffGaugeColorSettingsView()
    {
        InitializeComponent();
    }

    private BuffGaugeColorSettingsViewModel ViewModel => (BuffGaugeColorSettingsViewModel)DataContext;

    private void ColorOptionRadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ColorOptionViewModel option })
        {
            option.Select();
        }
    }

    private void ColorPickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: string gaugeKey })
        {
            return;
        }

        var owner = Window.GetWindow(this);
        var window = new ColorPickerWindow(ViewModel.GetSelectedColor(gaugeKey));
        if (owner is not null)
        {
            window.Owner = owner;
        }

        OwnerModalWindow.Show(window, owner, () =>
        {
            if (window.IsConfirmed)
            {
                ViewModel.ApplyColor(gaugeKey, window.SelectedColor);
            }
        });
    }
}
