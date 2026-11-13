// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System;
using System.Globalization;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Controls;

[TemplatePart(Name = PartSvCanvas, Type = typeof(Rectangle))]
[TemplatePart(Name = PartSvThumb, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartHueBar, Type = typeof(Rectangle))]
[TemplatePart(Name = PartHueThumb, Type = typeof(FrameworkElement))]
[TemplatePart(Name = PartHexTextBox, Type = typeof(TextBox))]
public class ColorPicker : Control
{
    private const string PartSvCanvas = "PART_SV_Canvas";
    private const string PartSvThumb = "PART_SV_Thumb";
    private const string PartHueBar = "PART_Hue_Bar";
    private const string PartHueThumb = "PART_Hue_Thumb";
    private const string PartHexTextBox = "PART_HexTextBox";

    private Rectangle? _svCanvas;
    private FrameworkElement? _svThumb;
    private Rectangle? _hueBar;
    private FrameworkElement? _hueThumb;
    private TextBox? _hexTextBox;
    private Button? _copyButton;

    private UIElement? _svInputElement;
    private UIElement? _hueInputElement;

    private bool _isDraggingSv;
    private bool _isDraggingHue;
    private bool _isUpdatingHexText;
    private bool _isApplyingColorFromHsv;
    private bool _isVisualUpdateQueued;

    private double _hue;
    private double _saturation;
    private double _value;


    public ColorPicker()
    {
        Focusable = true;
        Loaded += ColorPicker_Loaded;
        Unloaded += ColorPicker_Unloaded;
    }

    public static readonly DependencyProperty SelectedColorProperty =
        DependencyProperty.Register(
            nameof(SelectedColor),
            typeof(Color),
            typeof(ColorPicker),
            new FrameworkPropertyMetadata(
                Color.FromRgb(0x4D, 0xA3, 0xFF),
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedColorChanged));

    public Color SelectedColor
    {
        get => (Color)GetValue(SelectedColorProperty);
        set => SetValue(SelectedColorProperty, value);
    }

    public bool IsHexTextInputFocused => _hexTextBox?.IsKeyboardFocusWithin == true;

    public void CommitTextInput()
    {
        ApplyHexInput();
    }

    public override void OnApplyTemplate()
    {
        UnhookTemplatePartEvents();

        base.OnApplyTemplate();

        _svCanvas = GetTemplateChild(PartSvCanvas) as Rectangle;
        _svThumb = GetTemplateChild(PartSvThumb) as FrameworkElement;
        _hueBar = GetTemplateChild(PartHueBar) as Rectangle;
        _hueThumb = GetTemplateChild(PartHueThumb) as FrameworkElement;
        _hexTextBox = GetTemplateChild(PartHexTextBox) as TextBox;

        _svInputElement = _svCanvas?.Parent as UIElement ?? _svCanvas;
        _hueInputElement = _hueBar?.Parent as UIElement ?? _hueBar;

        HookTemplatePartEvents();

        UpdateHsvFromColor(SelectedColor);
        UpdateAllVisuals();
        QueueVisualUpdate();
    }

    private static void OnSelectedColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ColorPicker picker || e.NewValue is not Color color)
        {
            return;
        }

        if (!picker._isApplyingColorFromHsv)
        {
            picker.UpdateHsvFromColor(color);
        }

        picker.UpdateAllVisuals();
    }

    private void ColorPicker_Loaded(object sender, RoutedEventArgs e)
    {
        QueueVisualUpdate();
    }

    private void ColorPicker_Unloaded(object sender, RoutedEventArgs e)
    {
        StopDragging();
    }

    private void HookTemplatePartEvents()
    {
        if (_svInputElement != null)
        {
            _svInputElement.MouseLeftButtonDown += SvInputElement_MouseLeftButtonDown;
            _svInputElement.MouseMove += SvInputElement_MouseMove;
            _svInputElement.MouseLeftButtonUp += SvInputElement_MouseLeftButtonUp;
            _svInputElement.MouseLeave += SvInputElement_MouseLeave;
        }

        if (_hueInputElement != null)
        {
            _hueInputElement.MouseLeftButtonDown += HueInputElement_MouseLeftButtonDown;
            _hueInputElement.MouseMove += HueInputElement_MouseMove;
            _hueInputElement.MouseLeftButtonUp += HueInputElement_MouseLeftButtonUp;
            _hueInputElement.MouseLeave += HueInputElement_MouseLeave;
        }

        if (_svCanvas != null)
        {
            _svCanvas.SizeChanged += Part_SizeChanged;
        }

        if (_hueBar != null)
        {
            _hueBar.SizeChanged += Part_SizeChanged;
        }

        if (_hexTextBox != null)
        {
            _hexTextBox.PreviewKeyDown += HexTextBox_PreviewKeyDown;
            _hexTextBox.PreviewTextInput += HexTextBox_PreviewTextInput;
            _hexTextBox.LostFocus += HexTextBox_LostFocus;
            DataObject.AddPastingHandler(_hexTextBox, HexTextBox_Pasting);

            _hexTextBox.ApplyTemplate();
            _copyButton = _hexTextBox.Template.FindName("PART_CopyButton", _hexTextBox) as Button;
            if (_copyButton != null)
            {
                _copyButton.Click += CopyButton_Click;
            }
        }
    }

    private void UnhookTemplatePartEvents()
    {
        if (_svInputElement != null)
        {
            _svInputElement.MouseLeftButtonDown -= SvInputElement_MouseLeftButtonDown;
            _svInputElement.MouseMove -= SvInputElement_MouseMove;
            _svInputElement.MouseLeftButtonUp -= SvInputElement_MouseLeftButtonUp;
            _svInputElement.MouseLeave -= SvInputElement_MouseLeave;
        }

        if (_hueInputElement != null)
        {
            _hueInputElement.MouseLeftButtonDown -= HueInputElement_MouseLeftButtonDown;
            _hueInputElement.MouseMove -= HueInputElement_MouseMove;
            _hueInputElement.MouseLeftButtonUp -= HueInputElement_MouseLeftButtonUp;
            _hueInputElement.MouseLeave -= HueInputElement_MouseLeave;
        }

        if (_svCanvas != null)
        {
            _svCanvas.SizeChanged -= Part_SizeChanged;
        }

        if (_hueBar != null)
        {
            _hueBar.SizeChanged -= Part_SizeChanged;
        }

        if (_copyButton != null)
        {
            _copyButton.Click -= CopyButton_Click;
            _copyButton = null;
        }

        if (_hexTextBox != null)
        {
            _hexTextBox.PreviewKeyDown -= HexTextBox_PreviewKeyDown;
            _hexTextBox.PreviewTextInput -= HexTextBox_PreviewTextInput;
            _hexTextBox.LostFocus -= HexTextBox_LostFocus;
            DataObject.RemovePastingHandler(_hexTextBox, HexTextBox_Pasting);
        }

        StopDragging();
    }

    private void Part_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        QueueVisualUpdate();
    }

    private void SvInputElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_svInputElement == null)
        {
            return;
        }

        _isDraggingSv = true;
        _svInputElement.CaptureMouse();
        UpdateSvFromPoint(e.GetPosition(_svInputElement));
        e.Handled = true;
    }

    private void SvInputElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSv || _svInputElement == null)
        {
            return;
        }

        UpdateSvFromPoint(e.GetPosition(_svInputElement));
        e.Handled = true;
    }

    private void SvInputElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_svInputElement == null)
        {
            return;
        }

        _isDraggingSv = false;
        _svInputElement.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void SvInputElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDraggingSv || _svInputElement == null || e.LeftButton == MouseButtonState.Pressed)
        {
            return;
        }

        _isDraggingSv = false;
        _svInputElement.ReleaseMouseCapture();
    }

    private void HueInputElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_hueInputElement == null)
        {
            return;
        }

        _isDraggingHue = true;
        _hueInputElement.CaptureMouse();
        UpdateHueFromPoint(e.GetPosition(_hueInputElement));
        e.Handled = true;
    }

    private void HueInputElement_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingHue || _hueInputElement == null)
        {
            return;
        }

        UpdateHueFromPoint(e.GetPosition(_hueInputElement));
        e.Handled = true;
    }

    private void HueInputElement_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_hueInputElement == null)
        {
            return;
        }

        _isDraggingHue = false;
        _hueInputElement.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HueInputElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isDraggingHue || _hueInputElement == null || e.LeftButton == MouseButtonState.Pressed)
        {
            return;
        }

        _isDraggingHue = false;
        _hueInputElement.ReleaseMouseCapture();
    }

    private void StopDragging()
    {
        _isDraggingSv = false;
        _isDraggingHue = false;

        if (_svInputElement?.IsMouseCaptured == true)
        {
            _svInputElement.ReleaseMouseCapture();
        }

        if (_hueInputElement?.IsMouseCaptured == true)
        {
            _hueInputElement.ReleaseMouseCapture();
        }
    }

    private void UpdateSvFromPoint(Point point)
    {
        var width = Math.Max(1.0, GetSvWidth());
        var height = Math.Max(1.0, GetSvHeight());

        var x = Clamp(point.X, 0.0, width);
        var y = Clamp(point.Y, 0.0, height);

        _saturation = x / width;
        _value = 1.0 - y / height;

        ApplyCurrentHsvToSelectedColor();
    }

    private void UpdateHueFromPoint(Point point)
    {
        var height = Math.Max(1.0, GetHueHeight());
        var y = Clamp(point.Y, 0.0, height);

        _hue = (1.0 - y / height) * 360.0;
        if (_hue >= 360.0)
        {
            _hue = 359.999;
        }

        ApplyCurrentHsvToSelectedColor();
    }

    private void ApplyCurrentHsvToSelectedColor()
    {
        var newColor = ColorFromHsv(_hue, _saturation, _value);
        var oldColor = SelectedColor;

        try
        {
            _isApplyingColorFromHsv = true;
            SetCurrentValue(SelectedColorProperty, newColor);
        }
        finally
        {
            _isApplyingColorFromHsv = false;
        }

        if (oldColor.Equals(newColor))
        {
            UpdateAllVisuals();
        }
    }

    private void UpdateAllVisuals()
    {
        UpdateSvBaseColor();
        UpdateThumbPositions();
        UpdateCursorBrushes();
        UpdateHexTextFromSelectedColor();
    }

    private void QueueVisualUpdate()
    {
        if (_isVisualUpdateQueued)
        {
            return;
        }

        _isVisualUpdateQueued = true;

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _isVisualUpdateQueued = false;
            UpdateAllVisuals();
        }), DispatcherPriority.Loaded);
    }

    private void UpdateSvBaseColor()
    {
        if (_svCanvas == null)
        {
            return;
        }

        var brush = new SolidColorBrush(ColorFromHsv(_hue, 1.0, 1.0));
        brush.Freeze();
        _svCanvas.Fill = brush;
    }

    private void UpdateCursorBrushes()
    {
        var cursorColor = ColorUtilities.GetReadableTextColor(SelectedColor);
        var cursorBrush = new SolidColorBrush(cursorColor);
        cursorBrush.Freeze();

        if (_svThumb is Shape svShape)
        {
            svShape.Stroke = cursorBrush;
        }
    }

    private void UpdateThumbPositions()
    {
        if (_svThumb != null)
        {
            var width = Math.Max(1.0, GetSvWidth());
            var height = Math.Max(1.0, GetSvHeight());
            var thumbWidth = GetElementWidth(_svThumb);
            var thumbHeight = GetElementHeight(_svThumb);

            var x = _saturation * width;
            var y = (1.0 - _value) * height;

            Canvas.SetLeft(_svThumb, x - thumbWidth / 2.0);
            Canvas.SetTop(_svThumb, y - thumbHeight / 2.0);
        }

        if (_hueThumb != null)
        {
            var height = Math.Max(1.0, GetHueHeight());
            var thumbHeight = GetElementHeight(_hueThumb);
            var y = (1.0 - _hue / 360.0) * height;

            Canvas.SetTop(_hueThumb, y - thumbHeight / 2.0);
        }
    }

    private double GetSvWidth()
    {
        if (_svInputElement is FrameworkElement fe && fe.ActualWidth > 0)
        {
            return fe.ActualWidth;
        }

        if (_svCanvas?.ActualWidth > 0)
        {
            return _svCanvas.ActualWidth;
        }

        if (_svCanvas?.Width > 0)
        {
            return _svCanvas.Width;
        }

        return 1.0;
    }

    private double GetSvHeight()
    {
        if (_svInputElement is FrameworkElement fe && fe.ActualHeight > 0)
        {
            return fe.ActualHeight;
        }

        if (_svCanvas?.ActualHeight > 0)
        {
            return _svCanvas.ActualHeight;
        }

        if (_svCanvas?.Height > 0)
        {
            return _svCanvas.Height;
        }

        return 1.0;
    }

    private double GetHueHeight()
    {
        if (_hueInputElement is FrameworkElement fe && fe.ActualHeight > 0)
        {
            return fe.ActualHeight;
        }

        if (_hueBar?.ActualHeight > 0)
        {
            return _hueBar.ActualHeight;
        }

        if (_hueBar?.Height > 0)
        {
            return _hueBar.Height;
        }

        return 1.0;
    }

    private static double GetElementWidth(FrameworkElement element)
    {
        if (element.ActualWidth > 0)
        {
            return element.ActualWidth;
        }

        if (element.Width > 0)
        {
            return element.Width;
        }

        return 0.0;
    }

    private static double GetElementHeight(FrameworkElement element)
    {
        if (element.ActualHeight > 0)
        {
            return element.ActualHeight;
        }

        if (element.Height > 0)
        {
            return element.Height;
        }

        return 0.0;
    }

    private void UpdateHsvFromColor(Color color)
    {
        RgbToHsv(color, _hue, out var hue, out var saturation, out var value);

        _hue = hue;
        _saturation = saturation;
        _value = value;
    }

    private void UpdateHexTextFromSelectedColor()
    {
        var hex = $"#{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";

        if (_hexTextBox == null)
        {
            return;
        }

        if (_hexTextBox.Text == hex)
        {
            return;
        }

        try
        {
            _isUpdatingHexText = true;
            _hexTextBox.Text = hex;
            _hexTextBox.CaretIndex = _hexTextBox.Text.Length;
        }
        finally
        {
            _isUpdatingHexText = false;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_hexTextBox == null)
        {
            return;
        }

        ApplyHexInput();

        try
        {
            Clipboard.SetText(_hexTextBox.Text);
        }
        catch
        {
            // Clipboard access can fail when another process owns it. Ignore silently.
        }

        e.Handled = true;
    }

    private void HexTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_hexTextBox == null)
        {
            return;
        }

        if (IsSpaceKey(e))
        {
            e.Handled = true;
            PlayInvalidInputSound();
            return;
        }

        if (e.Key == Key.Enter)
        {
            ApplyHexInput();
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private static bool IsSpaceKey(KeyEventArgs e)
    {
        var key = e.Key;

        if (key == Key.ImeProcessed)
        {
            key = e.ImeProcessedKey;
        }
        else if (key == Key.System)
        {
            key = e.SystemKey;
        }

        return key == Key.Space;
    }

    private void HexTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!CanInsertHexText(e.Text))
        {
            e.Handled = true;
            PlayInvalidInputSound();
        }
    }

    private void HexTextBox_Pasting(object sender, DataObjectPastingEventArgs e)
    {
        if (!e.DataObject.GetDataPresent(DataFormats.Text)
            || e.DataObject.GetData(DataFormats.Text) is not string pastedText
            || !CanInsertHexText(pastedText))
        {
            e.CancelCommand();
            PlayInvalidInputSound();
        }
    }

    private void HexTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingHexText)
        {
            return;
        }

        ApplyHexInput();
    }

    private void ApplyHexInput()
    {
        if (_hexTextBox == null)
        {
            return;
        }

        if (!TryNormalizeHexInput(_hexTextBox.Text, out var normalizedHex)
            || !TryParseHex6(normalizedHex, out var color))
        {
            PlayInvalidInputSound();
            return;
        }

        try
        {
            _isUpdatingHexText = true;
            SetCurrentValue(SelectedColorProperty, color);
            _hexTextBox.Text = normalizedHex;
            _hexTextBox.CaretIndex = _hexTextBox.Text.Length;
        }
        finally
        {
            _isUpdatingHexText = false;
        }
    }

    private bool CanInsertHexText(string inputText)
    {
        if (_hexTextBox == null || string.IsNullOrEmpty(inputText))
        {
            return false;
        }

        var proposedText = CreateProposedHexText(inputText);
        if (!IsLegalHexInputState(proposedText))
        {
            return false;
        }

        if (!inputText.Contains('#'))
        {
            return true;
        }

        var textWithoutSelection = _hexTextBox.Text.Remove(_hexTextBox.SelectionStart, _hexTextBox.SelectionLength);
        return string.IsNullOrEmpty(textWithoutSelection);
    }

    private string CreateProposedHexText(string inputText)
    {
        if (_hexTextBox == null)
        {
            return inputText;
        }

        var text = _hexTextBox.Text;
        var selectionStart = Math.Clamp(_hexTextBox.SelectionStart, 0, text.Length);
        var selectionLength = Math.Clamp(_hexTextBox.SelectionLength, 0, text.Length - selectionStart);

        return text.Remove(selectionStart, selectionLength).Insert(selectionStart, inputText);
    }

    private static bool TryNormalizeHexInput(string raw, out string normalizedHex)
    {
        normalizedHex = string.Empty;

        if (!IsLegalHexInputState(raw))
        {
            return false;
        }

        var text = raw.StartsWith("#", StringComparison.Ordinal)
            ? raw[1..]
            : raw;

        normalizedHex = "#" + text.ToUpperInvariant().PadLeft(6, '0');
        return true;
    }

    private static bool IsLegalHexInputState(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return true;
        }

        var digitStartIndex = 0;
        if (text[0] == '#')
        {
            digitStartIndex = 1;
        }

        if (text.IndexOf('#', digitStartIndex) >= 0)
        {
            return false;
        }

        var digitCount = text.Length - digitStartIndex;
        if (digitCount > 6)
        {
            return false;
        }

        for (var i = digitStartIndex; i < text.Length; i++)
        {
            if (!IsHexChar(text[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryParseHex6(string normalizedHex, out Color color)
    {
        color = Colors.White;

        var text = normalizedHex.StartsWith("#", StringComparison.Ordinal)
            ? normalizedHex[1..]
            : normalizedHex;

        if (text.Length != 6)
        {
            return false;
        }

        if (!byte.TryParse(text.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)
            || !byte.TryParse(text.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)
            || !byte.TryParse(text.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        color = Color.FromRgb(r, g, b);
        return true;
    }

    private static bool IsHexChar(char ch)
    {
        return ch is >= '0' and <= '9'
               or >= 'A' and <= 'F'
               or >= 'a' and <= 'f';
    }

    private static void PlayInvalidInputSound()
    {
        SystemSounds.Beep.Play();
    }

    private static double Clamp(double value, double min, double max)
    {
        if (value < min)
        {
            return min;
        }

        if (value > max)
        {
            return max;
        }

        return value;
    }

    private static Color ColorFromHsv(double hue, double saturation, double value)
    {
        hue %= 360.0;
        if (hue < 0.0)
        {
            hue += 360.0;
        }

        saturation = Clamp(saturation, 0.0, 1.0);
        value = Clamp(value, 0.0, 1.0);

        var c = value * saturation;
        var x = c * (1.0 - Math.Abs(hue / 60.0 % 2.0 - 1.0));
        var m = value - c;

        double r1;
        double g1;
        double b1;

        if (hue < 60.0)
        {
            r1 = c;
            g1 = x;
            b1 = 0.0;
        }
        else if (hue < 120.0)
        {
            r1 = x;
            g1 = c;
            b1 = 0.0;
        }
        else if (hue < 180.0)
        {
            r1 = 0.0;
            g1 = c;
            b1 = x;
        }
        else if (hue < 240.0)
        {
            r1 = 0.0;
            g1 = x;
            b1 = c;
        }
        else if (hue < 300.0)
        {
            r1 = x;
            g1 = 0.0;
            b1 = c;
        }
        else
        {
            r1 = c;
            g1 = 0.0;
            b1 = x;
        }

        var r = (byte)Math.Round((r1 + m) * 255.0);
        var g = (byte)Math.Round((g1 + m) * 255.0);
        var b = (byte)Math.Round((b1 + m) * 255.0);

        return Color.FromRgb(r, g, b);
    }

    private static void RgbToHsv(Color color, double preserveHueWhenUndefined, out double hue, out double saturation, out double value)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var delta = max - min;

        if (delta > 0.00001)
        {
            if (Math.Abs(max - r) < 0.00001)
            {
                hue = 60.0 * (((g - b) / delta) % 6.0);
            }
            else if (Math.Abs(max - g) < 0.00001)
            {
                hue = 60.0 * (((b - r) / delta) + 2.0);
            }
            else
            {
                hue = 60.0 * (((r - g) / delta) + 4.0);
            }

            if (hue < 0.0)
            {
                hue += 360.0;
            }
        }
        else
        {
            hue = preserveHueWhenUndefined;
        }

        saturation = max <= 0.0 ? 0.0 : delta / max;
        value = max;
    }
}