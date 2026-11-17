// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.Controls;

/// <summary>
/// ウィジェットのフッターの「見出し: 値」の1組。見出しは文字の幅、数字は決まった幅の欄に右寄せ、単位はその後ろ。
/// 見出しのコロンから数字の右端までの間隔が数字の欄の幅(<see cref="NumberWidth"/>、使う側の XAML に書く)になり、
/// 言語と単位に関係なく同じになる。フッターの「見出し: 値」は全部これで作る。
/// </summary>
public partial class FooterValuePair : UserControl
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label),
        typeof(string),
        typeof(FooterValuePair),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(FooterValue),
        typeof(FooterValuePair),
        new PropertyMetadata(FooterValue.Empty));

    public static readonly DependencyProperty NumberWidthProperty = DependencyProperty.Register(
        nameof(NumberWidth),
        typeof(double),
        typeof(FooterValuePair),
        new PropertyMetadata(double.NaN));

    public static readonly DependencyProperty ReservesNumberUnitWidthProperty = DependencyProperty.Register(
        nameof(ReservesNumberUnitWidth),
        typeof(bool),
        typeof(FooterValuePair),
        new PropertyMetadata(false));

    public FooterValuePair()
    {
        InitializeComponent();
    }

    /// <summary>見出し(コロンまで)。</summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public FooterValue Value
    {
        get => (FooterValue)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>数字の欄の幅。見出しのコロンから数字の右端までの間隔になる。</summary>
    public double NumberWidth
    {
        get => (double)GetValue(NumberWidthProperty);
        set => SetValue(NumberWidthProperty, value);
    }

    /// <summary>
    /// 単位の欄を、全体設定「数値」の単位(K・M・B・T・万・億・兆)のうちいちばん広い幅で取っておくか。
    /// 値の大きさで単位が変わるメーターで、単位が変わるたびに後ろの組が左右に動かないようにする。
    /// </summary>
    public bool ReservesNumberUnitWidth
    {
        get => (bool)GetValue(ReservesNumberUnitWidthProperty);
        set => SetValue(ReservesNumberUnitWidthProperty, value);
    }
}
