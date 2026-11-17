// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>マップの印が指すもの。</summary>
public enum MapMarkerKind
{
    /// <summary>プレイヤー(自分・周りのプレイヤー・パーティの人)。番号は UID。</summary>
    Player,

    /// <summary>エンティティリストに載る実体。番号は実体の UUID。</summary>
    Entity,

    /// <summary>チャットで共有された位置。窓に1つだけなので番号は 0。</summary>
    SharedPosition,

    /// <summary>ダンジョンで隊長が置いたフィールドマーク。番号は置いた人の UUID × 8 ＋ マークの番号(1〜6)。</summary>
    FieldMark
}

/// <summary>印の鍵。同じ相手は、位置が届く経路が替わっても(周りにいる・パーティの同期)同じ印を使う。</summary>
public readonly record struct MapMarkerKey(MapMarkerKind Kind, long Id);

/// <summary>印の出し方。</summary>
public enum MapMarkerShape
{
    /// <summary>アイコン(クラスか敵の種類の絵)。</summary>
    Icon,

    /// <summary>簡略化した小さい円。</summary>
    Circle,

    /// <summary>簡略化した矢先。自分だけで、向きに回す。</summary>
    Arrow,

    /// <summary>絵をそのまま出す(色を変えない)。共有された位置の印。</summary>
    Image,

    /// <summary>フィールドマークの番号の絵をそのまま出す(色を変えない)。</summary>
    FieldMark
}

/// <summary>
/// マップの印1つ(アイコンと名前)。行は作り直さず値だけ入れ替える。
/// 位置は地図の層の中の位置(整数に丸めてある)で、層ごと窓の中へずらして出す(<c>MapWidgetViewModel</c>)。
/// </summary>
public sealed partial class MapMarkerEntry(MapMarkerKey key) : ObservableObject
{
    public MapMarkerKey Key { get; } = key;

    /// <summary>プレイヤーの印か。左クリックでその人のプレイヤー情報を開く(指の形のカーソル)。</summary>
    public bool IsPlayer => Key.Kind == MapMarkerKind.Player;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    /// <summary>名前。名前の欄を出さない印でも入れる(アイコンの TIPS に出す)。</summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>アイコンの下に名前の欄を出すか(表示設定「プレイヤー名」「エンティティ名」と「交戦中の名前非表示」)。出さない印はアイコンだけ。</summary>
    [ObservableProperty]
    private bool _showsName;

    /// <summary>
    /// 印の出し方。エンティティはボスだけアイコンでほかは円。プレイヤーは、表示設定「交戦中のプレイヤーを簡略化表示」のときの戦闘状態のプレイヤーと、
    /// 「自分を常に簡略化表示」のときの自分を簡略化する。
    /// 簡略化した印はアイコンの代わりに小さい形で、自分は向きを指す矢先、ほかは円。プレイヤーの円と矢先は白い縁で、縁の無い敵の円と見分ける。
    /// </summary>
    [ObservableProperty]
    private MapMarkerShape _shape;

    /// <summary>矢先の向き(度)。北が 0 の時計回りで、マップの上が北。</summary>
    [ObservableProperty]
    private double _facingDegrees;

    /// <summary>アイコンの形(型抜き)。</summary>
    [ObservableProperty]
    private Brush? _iconMask;

    /// <summary>アイコンの色(設定「アイコンカラー」)。</summary>
    [ObservableProperty]
    private Brush? _iconBrush;

    /// <summary>そのまま出す絵(<see cref="MapMarkerShape.Image"/> と <see cref="MapMarkerShape.FieldMark"/> のときだけ)。色は変えない。</summary>
    [ObservableProperty]
    private ImageSource? _iconImage;

    /// <summary>
    /// 得点(「○○pt」)。実体の印だけで、出す得点が無ければ空。ボスのアイコンはエンティティリストのアイコンと同じ決まりで中央に重ね、
    /// 円は交戦していない間だけ円の上に出す(交戦中の得点はフッターの「交戦中」に入る)。
    /// </summary>
    [ObservableProperty]
    private string _scoreText = string.Empty;

    /// <summary>重なりの順。大きいほど手前(フィールドマーク → 実体 → 周りのプレイヤー → パーティの人 → 自分)。</summary>
    [ObservableProperty]
    private int _zIndex;
}
