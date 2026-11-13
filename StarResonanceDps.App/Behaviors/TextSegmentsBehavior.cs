// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Behaviors;

/// <summary>
/// 色を指定した文字。<see cref="TextSegmentsBehavior"/> が <see cref="Run.Foreground"/> に当てる。
/// 色を持たない素の文字列は <see cref="TextBlock"/> の色のまま出る。
/// </summary>
public sealed record TextRunSegment(string Text, Brush Foreground);

/// <summary>押すと既定のブラウザで開くリンクの文字。<see cref="TextSegmentsBehavior"/> が <see cref="Hyperlink"/> にする。</summary>
public sealed record TextLinkSegment(string Text, Uri Uri);

/// <summary>
/// 文字とアイコンを混ぜた1行を <see cref="TextBlock"/> に流し込む。
///
/// <para>
/// 素の文字列と色つきの文字(<see cref="TextRunSegment"/>)は <see cref="Run"/>、リンク(<see cref="TextLinkSegment"/>)は <see cref="Hyperlink"/>
/// (見た目は <see cref="LinkStyleProperty"/>、押したら <see cref="ExternalLinkOpener"/> で開く)、それ以外は <see cref="InlineUIContainer"/> の中の
/// <see cref="ContentPresenter"/> になり、型ごとの <see cref="DataTemplate"/>(<c>DataType</c> 指定)で描かれる。TextBlock のまま組むので、文字の省略表示が効く。
/// </para>
/// </summary>
public static class TextSegmentsBehavior
{
    public static readonly DependencyProperty SegmentsProperty = DependencyProperty.RegisterAttached(
        "Segments",
        typeof(IEnumerable),
        typeof(TextSegmentsBehavior),
        new PropertyMetadata(null, OnSegmentsChanged));

    /// <summary>リンクの <see cref="Hyperlink"/> に当てるスタイル。変わったら行を組み直す。</summary>
    public static readonly DependencyProperty LinkStyleProperty = DependencyProperty.RegisterAttached(
        "LinkStyle",
        typeof(Style),
        typeof(TextSegmentsBehavior),
        new PropertyMetadata(null, OnSegmentsChanged));

    public static IEnumerable? GetSegments(DependencyObject obj)
    {
        return (IEnumerable?)obj.GetValue(SegmentsProperty);
    }

    public static void SetSegments(DependencyObject obj, IEnumerable? value)
    {
        obj.SetValue(SegmentsProperty, value);
    }

    public static Style? GetLinkStyle(DependencyObject obj)
    {
        return (Style?)obj.GetValue(LinkStyleProperty);
    }

    public static void SetLinkStyle(DependencyObject obj, Style? value)
    {
        obj.SetValue(LinkStyleProperty, value);
    }

    private static void OnSegmentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock textBlock)
        {
            throw new InvalidOperationException($"{nameof(TextSegmentsBehavior)} can only be attached to a TextBlock.");
        }

        textBlock.Inlines.Clear();
        if (GetSegments(textBlock) is not IEnumerable segments)
        {
            return;
        }

        foreach (var segment in segments)
        {
            textBlock.Inlines.Add(segment switch
            {
                string text => new Run(text),
                TextRunSegment run => new Run(run.Text) { Foreground = run.Foreground },
                TextLinkSegment link => CreateHyperlink(link, GetLinkStyle(textBlock)),
                _ => new InlineUIContainer(new ContentPresenter { Content = segment })
                {
                    BaselineAlignment = BaselineAlignment.Center
                }
            });
        }
    }

    private static Hyperlink CreateHyperlink(TextLinkSegment link, Style? style)
    {
        var hyperlink = new Hyperlink(new Run(link.Text))
        {
            NavigateUri = link.Uri,
            Style = style
        };
        hyperlink.Click += (_, _) => ExternalLinkOpener.Open(link.Uri.AbsoluteUri);
        return hyperlink;
    }
}
