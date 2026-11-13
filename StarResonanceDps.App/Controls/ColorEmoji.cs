// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// 絵文字1文字を、書体の色の層で色つきに描く(<see cref="ColorEmojiDrawings"/>)。文字の行の中に置いて使う。
/// 大きさ・書体・文字の色は、置いた場所の文字の設定をそのまま受け継ぐ。
/// 色の層が使えない絵文字と、文字の色が単色のブラシでないときは、受け継いだ書体の普通の文字(単色)で描く。
/// </summary>
public sealed class ColorEmoji : FrameworkElement
{
    public static readonly DependencyProperty EmojiProperty = DependencyProperty.Register(
        nameof(Emoji),
        typeof(string),
        typeof(ColorEmoji),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontSizeProperty = TextElement.FontSizeProperty.AddOwner(
        typeof(ColorEmoji),
        new FrameworkPropertyMetadata(
            SystemFonts.MessageFontSize,
            FrameworkPropertyMetadataOptions.Inherits
            | FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FontFamilyProperty = TextElement.FontFamilyProperty.AddOwner(
        typeof(ColorEmoji),
        new FrameworkPropertyMetadata(
            SystemFonts.MessageFontFamily,
            FrameworkPropertyMetadataOptions.Inherits
            | FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ForegroundProperty = TextElement.ForegroundProperty.AddOwner(
        typeof(ColorEmoji),
        new FrameworkPropertyMetadata(
            SystemColors.ControlTextBrush,
            FrameworkPropertyMetadataOptions.Inherits
            | FrameworkPropertyMetadataOptions.AffectsMeasure
            | FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>測ったときの絵。色の層が使えなければ null で、<see cref="_plainText"/> を描く。</summary>
    private ColorEmojiDrawing? _drawing;

    private FormattedText? _plainText;

    public string Emoji
    {
        get => (string)GetValue(EmojiProperty);
        set => SetValue(EmojiProperty, value);
    }

    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontFamily FontFamily
    {
        get => (FontFamily)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public Brush Foreground
    {
        get => (Brush)GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _drawing = null;
        _plainText = null;
        if (string.IsNullOrEmpty(Emoji))
        {
            return default;
        }

        // 文字の色で描く層があるので、絵は文字の色ごとに作る。単色でない文字の色では作らず、普通の文字で描く。
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        if (Foreground is SolidColorBrush foreground)
        {
            _drawing = ColorEmojiDrawings.Get(Emoji, FontSize, foreground.Color, pixelsPerDip);
            if (_drawing is not null)
            {
                return _drawing.Size;
            }
        }

        _plainText = new FormattedText(
            Emoji,
            CultureInfo.CurrentUICulture,
            FlowDirection,
            new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
            FontSize,
            Foreground,
            pixelsPerDip);
        return new Size(_plainText.WidthIncludingTrailingWhitespace, _plainText.Height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (_drawing is not null)
        {
            drawingContext.DrawDrawing(_drawing.Drawing);
        }
        else if (_plainText is not null)
        {
            drawingContext.DrawText(_plainText, default);
        }
    }
}
