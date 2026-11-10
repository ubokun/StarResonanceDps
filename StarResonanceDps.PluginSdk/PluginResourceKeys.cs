namespace StarResonanceDps.PluginSdk;

/// <summary>
/// Resource keys of the app's look that plugins may use, for example
/// <c>Background="{DynamicResource Brush.PanelBackground}"</c> or <c>Style="{StaticResource Button.Text}"</c>.
/// These names do not change. Keys not listed here belong to the app and may change at any time.
/// Use <c>DynamicResource</c> for brushes so that the plugin follows the app's theme.
/// </summary>
public static class PluginResourceKeys
{
    /// <summary>
    /// Brushes of the app's theme. All but <see cref="Success"/> are made from the window color of the theme and change with it.
    /// Inside a widget window they follow the app's theme, not the widget's color.
    /// </summary>
    public static class Brushes
    {
        /// <summary>The window color of the theme: the background of windows.</summary>
        public const string WindowSurface = "Brush.WindowSurface";

        /// <summary>A background one step away from the window color, for panels.</summary>
        public const string PanelBackground = "Brush.PanelBackground";

        /// <summary>Borders and separators.</summary>
        public const string Border = "Brush.Border";

        /// <summary>A fainter border than <see cref="Border"/>.</summary>
        public const string BorderSoft = "Brush.BorderSoft";

        /// <summary>The background of controls such as buttons.</summary>
        public const string ControlBackground = "Brush.ControlBackground";

        /// <summary>The background of a control under the mouse or selected.</summary>
        public const string ControlActiveBackground = "Brush.ControlActiveBackground";

        /// <summary>The background of input boxes.</summary>
        public const string InputBackground = "Brush.InputBackground";

        /// <summary>The background of an input box or a row under the mouse.</summary>
        public const string InputHoverBackground = "Brush.InputHoverBackground";

        /// <summary>The main text color, readable on the window color.</summary>
        public const string TextPrimary = "Brush.TextPrimary";

        /// <summary>A dimmer text color than <see cref="TextPrimary"/>, for notes and details.</summary>
        public const string TextSecondary = "Brush.TextSecondary";

        /// <summary>A dimmer text color than <see cref="TextSecondary"/>.</summary>
        public const string TextMuted = "Brush.TextMuted";

        /// <summary>Green for a good state. It does not change with the theme.</summary>
        public const string Success = "Brush.Success";
    }

    /// <summary>The app's font, and the text style of widgets.</summary>
    public static class Text
    {
        /// <summary>The app's font family (<c>FontFamily</c>).</summary>
        public const string ProjectDefaultFont = "Font.ProjectDefault";

        /// <summary>A <c>TextBlock</c> style for widgets: white text with a black shadow, in the app's font with tabular digits.</summary>
        public const string WidgetWindow = "Text.WidgetWindow";

        /// <summary><see cref="WidgetWindow"/> at size 13.</summary>
        public const string WidgetWindowSubtitle = "Text.WidgetWindowSubtitle";

        /// <summary>The black shadow (<c>DropShadowEffect</c>) of <see cref="WidgetWindow"/>.</summary>
        public const string WidgetWindowTextShadow = "Effect.WidgetWindowTextShadow";
    }

    /// <summary>Button and link styles.</summary>
    public static class Buttons
    {
        /// <summary>A <c>Button</c> style: a text button with the app's look.</summary>
        public const string Text = "Button.Text";

        /// <summary>A <c>Button</c> style for an icon: no border, with a background only under the mouse.</summary>
        public const string Icon = "Button.Icon";

        /// <summary>A <c>Hyperlink</c> style without WPF's default link colors: underlined, with a hand cursor, in the color of the surrounding text.</summary>
        public const string HyperlinkBase = "Hyperlink.Base";

        /// <summary>A <c>Hyperlink</c> style for a link in a sentence, in <see cref="Brushes.TextPrimary"/>.</summary>
        public const string HyperlinkInline = "Hyperlink.Inline";
    }

    /// <summary>Input styles.</summary>
    public static class Inputs
    {
        /// <summary>A single-line text box.</summary>
        public const string PathBox = "Input.PathBox";

        /// <summary>A <c>TextBox</c> style for several lines.</summary>
        public const string MultilineTextBox = "Input.MultilineTextBox";

        /// <summary>A <c>TextBox</c> style for a search box.</summary>
        public const string SearchBox = "Input.SearchBox";

        /// <summary>The <c>ComboBox</c> style of the settings screens.</summary>
        public const string SettingsComboBox = "Input.SettingsComboBox";

        /// <summary>The <c>ComboBoxItem</c> style of <see cref="SettingsComboBox"/>.</summary>
        public const string SettingsComboBoxItem = "Input.SettingsComboBoxItem";

        /// <summary>A <c>ComboBox</c> style. A list without items does not open.</summary>
        public const string SelectBox = "Input.SelectBox";

        /// <summary>A <c>ToggleButton</c> style, also for a <c>CheckBox</c>: an on/off switch.</summary>
        public const string Switch = "Input.Switch";

        /// <summary>A <c>Slider</c> style.</summary>
        public const string ValueSlider = "Input.ValueSlider";
    }

    /// <summary>Panel styles.</summary>
    public static class Panels
    {
        /// <summary>
        /// A <c>ContentControl</c> style with rounded corners and a 1-pixel border, transparent until <c>Background</c> and <c>BorderBrush</c> are set.
        /// </summary>
        public const string RoundedOutline = "Surface.RoundedOutline";

        /// <summary><see cref="RoundedOutline"/> with the smaller corners of buttons.</summary>
        public const string RoundedButtonOutline = "Surface.RoundedButtonOutline";

        /// <summary><see cref="RoundedOutline"/> with the panel background and border.</summary>
        public const string Root = "Panel.Root";

        /// <summary>A <c>ContentControl</c> style for a section of a settings screen.</summary>
        public const string SettingSection = "Panel.SettingSection";

        /// <summary>A <c>Border</c> style for a row of a settings section, with a line under it.</summary>
        public const string SettingField = "Panel.SettingField";

        /// <summary><see cref="SettingField"/> without the line, for the last row of a section.</summary>
        public const string SettingFieldLast = "Panel.SettingFieldLast";
    }

    /// <summary>Scroll viewer and scroll bar styles.</summary>
    public static class Scrolling
    {
        /// <summary>A <c>ScrollViewer</c> style with the scroll bar of the app's lists.</summary>
        public const string ScrollViewer = "ScrollViewer.WidgetList";

        /// <summary>
        /// A <c>ScrollViewer</c> style for a widget window: the scroll bar follows the widget's color and opacity. It works only inside a widget window.
        /// </summary>
        public const string WidgetWindowScrollViewer = "ScrollViewer.WidgetWindow";

        /// <summary>The <c>ScrollBar</c> style of the app's lists.</summary>
        public const string ScrollBar = "ScrollBar.WidgetList";

        /// <summary>The <c>ScrollBar</c> style of widget windows. The thumb is drawn in the scroll bar's <c>Background</c>, transparent by default.</summary>
        public const string WidgetWindowScrollBar = "ScrollBar.WidgetWindow";
    }

    /// <summary>Tooltip styles, and styles of elements that show a tooltip.</summary>
    public static class ToolTips
    {
        /// <summary>A <c>ToolTip</c> style with the app's look.</summary>
        public const string Info = "ToolTip.Info";

        /// <summary>
        /// A <c>ToolTip</c> style for a widget window, in the widget's color. It has no colors unless the element that shows it
        /// has the <c>DataContext</c> of the widget window (<c>Window</c>) in its <c>Tag</c>.
        /// </summary>
        public const string WidgetWindowInfo = "ToolTip.WidgetWindowInfo";

        /// <summary>The style of an element that shows a tooltip and does something when clicked.</summary>
        public const string Owner = "ToolTipOwner.Default";

        /// <summary>The style of an element that shows a tooltip and does nothing when clicked: the tooltip stays open on click.</summary>
        public const string OwnerKeepsOnClick = "ToolTipOwner.KeepsOnClick";
    }

    /// <summary>Corner radii and padding.</summary>
    public static class Sizes
    {
        /// <summary>The corner radius of panels and inputs (<c>CornerRadius</c>).</summary>
        public const string DefaultRadius = "Radius.Default";

        /// <summary>The corner radius of buttons (<c>CornerRadius</c>).</summary>
        public const string ButtonRadius = "Radius.Button";

        /// <summary>The horizontal padding of cards (<c>Thickness</c>).</summary>
        public const string CardPadding = "Padding.Card";
    }

    /// <summary>
    /// Icon geometries, and the styles of a <c>Path</c> that draws them: set <c>Style</c> to <see cref="StrokeStyle"/> or <see cref="FillStyle"/>
    /// and <c>Data</c> to a geometry.
    /// </summary>
    public static class Icons
    {
        /// <summary>The style of a <c>Path</c> that draws an icon geometry with strokes.</summary>
        public const string StrokeStyle = "Icon.Stroke";

        /// <summary>The style of a <c>Path</c> that draws an icon geometry filled.</summary>
        public const string FillStyle = "Icon.Fill";

        /// <summary>The search icon.</summary>
        public const string Search = "Icon.Search";

        /// <summary>The add icon.</summary>
        public const string Add = "Icon.Add";

        /// <summary>A downward chevron.</summary>
        public const string ChevronDown = "Icon.ChevronDown";

        /// <summary>A rightward chevron.</summary>
        public const string ChevronRight = "Icon.ChevronRight";

        /// <summary>The copy icon.</summary>
        public const string Copy = "Icon.Copy";

        /// <summary>The settings icon.</summary>
        public const string Settings = "Icon.Settings";

        /// <summary>The filter icon.</summary>
        public const string Filter = "Icon.Filter";

        /// <summary>The sort icon.</summary>
        public const string Sort = "Icon.Sort";

        /// <summary>The hide icon.</summary>
        public const string EyeOff = "Icon.EyeOff";

        /// <summary>The more icon (vertical dots).</summary>
        public const string MoreVertical = "Icon.MoreVertical";

        /// <summary>The help icon.</summary>
        public const string Help = "Icon.Help";

        /// <summary>The wrench icon.</summary>
        public const string Wrench = "Icon.Wrench";

        /// <summary>The sliders icon.</summary>
        public const string Sliders = "Icon.Sliders";
    }
}
