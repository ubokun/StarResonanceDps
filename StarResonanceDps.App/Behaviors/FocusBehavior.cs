// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Behaviors;

public static class FocusBehavior
{
    public static readonly DependencyProperty ClearFocusOnBackgroundClickProperty = DependencyProperty.RegisterAttached(
        "ClearFocusOnBackgroundClick",
        typeof(bool),
        typeof(FocusBehavior),
        new PropertyMetadata(false, OnClearFocusOnBackgroundClickChanged));

    public static bool GetClearFocusOnBackgroundClick(DependencyObject obj)
    {
        return (bool)obj.GetValue(ClearFocusOnBackgroundClickProperty);
    }

    public static void SetClearFocusOnBackgroundClick(DependencyObject obj, bool value)
    {
        obj.SetValue(ClearFocusOnBackgroundClickProperty, value);
    }

    private static void OnClearFocusOnBackgroundClickChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.MouseDown += OnMouseDown;
        }
        else
        {
            element.MouseDown -= OnMouseDown;
        }
    }

    private static void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
        {
            return;
        }

        if (IsInsideInteractiveElement(source))
        {
            return;
        }

        Keyboard.ClearFocus();

        if (sender is DependencyObject focusScopeSource)
        {
            var focusScope = FocusManager.GetFocusScope(focusScopeSource);
            FocusManager.SetFocusedElement(focusScope, null);
        }
    }

    /// <summary>押した所が操作する部品か、今キーボードの焦点を持つ要素の中か(焦点を外さない)。</summary>
    private static bool IsInsideInteractiveElement(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null; current = ElementTree.GetParent(current))
        {
            if (current is TextBoxBase
                or PasswordBox
                or ButtonBase
                or ComboBox
                or Selector
                or MenuItem
                or IInputElement { IsKeyboardFocused: true })
            {
                return true;
            }
        }

        return false;
    }
}
