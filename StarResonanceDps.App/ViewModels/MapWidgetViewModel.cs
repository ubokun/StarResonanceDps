// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Serilog;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// マップの窓(1枚だけ開く)。地図の画像の上に、位置の分かる相手の印(アイコンと名前)を出す。いつも自分のいるマップを出す。
///
/// <para>
/// 自分のいるマップ: 自分・周りのプレイヤー・そのマップにいるパーティの人・エンティティリストに載る実体を出す。
/// 見る中心は自分で、自分がマップを移動したら移動先のマップに替わる。自分の位置が届くまでは何も出さない。地図の画像の無いマップは印だけ。
/// 階層のあるマップは自分のいる階層の画像を出す(リンクで選んだ場所を見ている間はリンクの階層)。
/// </para>
///
/// <para>
/// 出す相手は表示設定「プレイヤー」「エンティティ」で絞る(決まりはプレイヤーリストとエンティティリストの「フィルター」と同じ。
/// <see cref="PartyStateSnapshot.ShouldInclude"/>・<see cref="EntityListEntry.IsShown"/>)。フッターの個数は絞った後の数。
/// 自分を出さない絞り方でも、見る中心は自分のまま。
/// 印の名前の欄を出す相手は表示設定「プレイヤー名」(決まりはフィルター「プレイヤー」と同じ)と「エンティティ名」で決め、出さない印はアイコンだけ。
/// 名前はどの印もアイコンの TIPS に出す。
/// エンティティはボスだけアイコン(ボスの印の絵)で、ほかは小さい円。色は全部アイコンカラー「エンティティ」。
/// 得点は、ボスはアイコンの中央(エンティティリストのアイコンと同じ決まり)、円は交戦していない間だけ円の上に出す。
/// プレイヤーはクラスアイコンで出し、表示設定「交戦中のプレイヤーを簡略化表示」なら戦闘状態のプレイヤーを、「自分を常に簡略化表示」なら自分を、
/// アイコンの代わりに小さい円(自分は向きを指す矢先)で出す(戦闘状態が届かない相手、つまり周りにいないパーティの人は戦闘状態でない扱い)。
/// 戦闘状態の相手は、表示設定「交戦中の名前非表示」の対象なら名前の欄を出さない。
/// </para>
///
/// <para>
/// 別のマップ(プレイヤー情報の現在地から、ほかのマップにいるパーティの人を選んだとき、<see cref="ShowPlayer"/>):
/// その人を中心に、そのマップにいるパーティの人を出す(周りのプレイヤーと実体は自分のいるマップの分しか届かない)。
/// 地図の無いマップは印だけ。画像は地上のもの(リンクで選んだ場所を見ている間はリンクの階層)。
/// <see cref="ReturnToCurrentMap"/>(ヘッダーのボタンと右クリックのメニュー)か、自分がそのマップへ移動したら、自分のいるマップに戻る。
/// </para>
///
/// <para>
/// 表示範囲(地図を出す領域の短い辺に入れるワールド座標の長さ)は、窓の上のホイールで変える(<see cref="ZoomByWheel"/>)。
/// 出せる範囲は地図ごとに決まり(<see cref="GetViewRangeLimits"/>)、いちばん広いときは地図の全体が入る。
/// 値は窓が開いている間だけ持ち、保存しない。開いたときは <see cref="DefaultViewRange"/>。別のマップへ移っても今の値のまま使う。
/// </para>
///
/// <para>
/// 見る中心は、地図の外が見えない位置へ寄せる(自分が地図の端に近いと、自分は領域の中央からずれる)。
/// 地図は掴んで動かせる(<see cref="PanBy"/>)。自分のいるマップでは、掴んで動かした場所(と、現在地から選んだ同じマップの人の位置)を
/// 自分が動くまで見続け、自分が動いたら自分に合わせ直す(<see cref="_manualSelfPosition"/>)。
/// <see cref="ResumeFollowingSelf"/> と自分のマップ移動でもすぐ合わせ直す。地図の無いマップは範囲が無いので、どの向きにも動かせる。
/// </para>
///
/// <para>
/// 印の左クリックで、プレイヤーの印はその人のプレイヤー情報を開き、敵の印はターゲット情報でその敵を映す(<see cref="OpenMarkerWindow"/>)。
/// </para>
///
/// <para>
/// チャットで共有された位置(<see cref="ShowSharedPosition"/>): その場所を見る中心にし、リンクの階層の地図を出して(ゲームも
/// リンクの階層の地図を開く)、ゲームと同じ絵の印を置く。リンクが別のマップか別の階層にあるときは見ている状態になり、
/// 自分が動いても・地図を掴んで動かしても・ダブルクリックしても中心と階層を戻さず、戻るのは <see cref="ReturnToCurrentMap"/> と
/// 自分のマップ移動だけ(<see cref="IsViewingSharedPosition"/>)。自分のいるマップの自分の階層のリンクと、自分がその階層へ移動した後は、
/// 地図を掴んだ後と同じ(層は自分に付いていき、自分が動いたら自分に合わせ直す)。
/// 印はその場所のあるマップを見ている間だけ出て(階層では隠さない)、新しいリンクを押したときは置き換え、
/// 右クリックのメニュー「位置のマークを消す」(<see cref="ClearSharedPosition"/>、見ている場所と階層はそのまま)とログアウトで消える。
/// マップ移動では消えない。
/// </para>
///
/// <para>
/// パーティの人がそのマップにいるかは、位置と一緒に届くパーティの同期のシーン番号だけで決める(階層とチャンネルでは外さない)。
/// 周りにいるパーティの人は、周りの実体として届く位置を先に使う。
/// </para>
///
/// <para>
/// 位置は位置の控え(<see cref="EntityPositionStore"/>)を、画面が出ている間の定期の更新(<see cref="Refresh"/>、画面のコードが呼ぶ)で読む。
/// 印は作り直さず値だけ入れ替え、窓の外の印は作らない。名前とアイコンは名簿か周りの実体の一覧が変わったときだけ引き直す。
/// 地図の画像と印は、原点が同じ(地図の左上)2つの層に置き、どちらも同じ量だけ窓の中へずらす
/// (見る中心が動いても、動いていない相手の印は値が変わらない)。
/// 画面は、地図の画像の層を窓の枠の層へ、印の層を中身の層へ置く。地図の画像は窓の背景と1枚の絵として窓の不透明度で合成され
/// (地図のある所では背景が地図に覆われ、地図の後ろに背景の色が出ない)、印はいつも不透明。
/// 位置とずらす量は整数に丸める(影つきの文字が画素の端数に置かれるとにじむ)。
/// </para>
///
/// <para>
/// 地図の画像は同じ地図を映す窓で共有し(<see cref="MapImageCache"/>)、映す地図が替わったときと窓を閉じたときに手放す。
/// </para>
/// </summary>
public sealed partial class MapWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource
{
    /// <summary>
    /// 自分のいるマップの窓を開いたときの表示範囲。周りの実体が届く範囲(半径を 100 と置く)の直径で、
    /// 自分の周りに届く実体が、地図を出す領域の短い辺にちょうど入る。
    /// </summary>
    private const double DefaultViewRange = 200d;

    /// <summary>表示範囲をいちばん狭くできる値。地図の画像の細かさに関係なく、どの地図もここまで拡大できる。</summary>
    private const double NarrowestViewRange = 20d;

    /// <summary>地図の無いマップで、表示範囲をいちばん広くできる値。地図のあるマップは地図の辺で決まる。</summary>
    private const double WidestViewRangeWithoutMap = 3200d;

    /// <summary>ホイール1刻みで表示範囲に掛ける倍率。奥へ回すとこの倍率で狭く、手前へ回すと広くなる。</summary>
    private const double WheelZoomFactor = 1.1d;

    /// <summary>地図が領域からこの長さより大きくはみ出している向きにだけ、掴んで動かせる。位置を整数に丸めると見えない差は数えない。</summary>
    private const double PanSlack = 0.5d;

    /// <summary>
    /// 共有された位置の印の絵の鍵(<c>Icons.xaml</c>)。ゲームがこの位置の追跡に出すアイコンと同じもの
    /// (<c>SceneTagTable</c> の <c>DynamicTrace</c> の <c>Icon1</c>)。
    /// </summary>
    private const string SharedPositionIconKey = "Icon.MapSharedPosition";

    /// <summary>フィールドマークの番号の絵の鍵(<c>Icons.xaml</c>)の頭。後ろにマークの番号(1〜6)を付ける。</summary>
    private const string FieldMarkIconKeyPrefix = "Icon.FieldMark";

    /// <summary>アプリの画像のフォルダに無かった地図の画像の名前。名前ごとに1回だけログに残す(画面のスレッドだけで使う)。</summary>
    private static readonly HashSet<string> LoggedMissingImageNames = new(StringComparer.OrdinalIgnoreCase);

    private readonly WidgetListItemViewModel _widget;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly EntityPositionStore _positions = EntityPositionStore.Instance;

    /// <summary>プレイヤーの窓を開く(窓の管理が渡す。蘇生リスト・被ダメログと同じ)。</summary>
    private readonly Action<WidgetKind, long> _requestPlayerWindow;

    /// <summary>ターゲット情報で敵を映す(窓の管理が渡す。エンティティリストの行の左クリックと同じ先)。</summary>
    private readonly Action<EntityTargetInfo> _showEntity;

    private readonly ObservableCollection<MapMarkerEntry> _markers = [];
    private readonly Dictionary<MapMarkerKey, MapMarkerEntry> _markersByKey = [];

    /// <summary>今回の更新で位置を当てた印。更新のたびに使い回す。</summary>
    private readonly HashSet<MapMarkerKey> _placedKeys = [];

    /// <summary>フィールドマークの控えの写し。更新のたびに使い回す。</summary>
    private readonly List<FieldMarkStore.FieldMark> _fieldMarks = [];

    private readonly Dictionary<string, SolidColorBrush> _iconBrushes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ImageBrush> _iconMasks = new(StringComparer.Ordinal);

    private MapWidgetSettingsConfig _settings;

    /// <summary>
    /// フッターの左を作った元(マップの名前とチャンネル)。同じなら作り直さない。
    /// 自分のいるマップはプレイヤーリストと同じ名簿の値、別のマップはそのマップの番号から引いた名前と選んだ人のチャンネル。
    /// </summary>
    private int _footerMapSourceSceneId = -1;
    private string? _footerMapSourceName;
    private uint _footerMapSourceChannel;

    /// <summary>フッターの右を作った値。同じなら文字を作り直さない(定期の更新のたびに文字を作らない)。</summary>
    private int _footerFirstValue = -1;
    private int? _footerSecondValue = -1;

    /// <summary>
    /// フッターの右を得点で出しているか。自分のいるマップを見ていて、今の場所が得点を求めている間(エンティティリストと同じ条件)。
    /// 偽の間は個数(エンティティ・プレイヤー)。
    /// </summary>
    private bool _isFooterScoreShown;

    /// <summary>設定・言語が変わった。次の更新でフッターの文字を作り直す。</summary>
    private bool _isFooterStale = true;

    /// <summary>前回の更新で、今の場所が得点を求めていたか。変わったら印の得点を引き直す。</summary>
    private bool _lastIsScoreRequested;

    /// <summary>前回の更新で見た、自分のいるマップの番号。変わったら自分がマップを移動した。</summary>
    private int _selfSceneId;

    /// <summary>別のマップを見ている間、そのマップの番号。自分のいるマップを見ている間は 0。</summary>
    private int _viewedSceneId;

    /// <summary>別のマップを見ている間、選んだ人のチャンネル(フッターに出す)。分からなければ 0。</summary>
    private uint _viewedLineId;

    /// <summary>
    /// チャットで共有された位置(<see cref="ShowSharedPosition"/>)。そのマップを見ている間だけ印を置く。
    /// 新しいリンクを押したら置き換え、右クリックのメニューとログアウトで捨てる。
    /// </summary>
    private MapSharedPosition? _sharedPosition;

    /// <summary>リンクで選んだ場所を見ている間(<see cref="IsViewingSharedPosition"/>)の階層。印を消しても見ている間は残る。</summary>
    private int _sharedPositionViewLayer;

    private double _viewportWidth;
    private double _viewportHeight;

    /// <summary>印が窓の端からはみ出して見える幅。窓の外でも、この幅の内側にある印は作る。</summary>
    private double _markerOverflow;

    /// <summary>
    /// 望んだ表示範囲。窓が開いている間だけ持つ。使うときにその地図で出せる範囲に収め、収めた値は書き戻さない
    /// (狭い地図から広い地図へ移ると、望んだ値に戻る)。
    /// </summary>
    private double _viewRange = DefaultViewRange;

    /// <summary>
    /// 見る中心を自分に合わせ続けるか。自分のいるマップを見ている間だけ真になる。
    /// 地図を掴んで動かす・現在地から同じマップの人を選ぶと偽になり、自分が動いたとき(<see cref="_manualSelfPosition"/>)・
    /// <see cref="ResumeFollowingSelf"/>・自分のマップ移動で真に戻る。
    /// </summary>
    private bool _isFollowingSelf = true;

    /// <summary>地図を掴んで動かしている(マウスを押したまま)。この間は自分が動いても合わせ直さない。</summary>
    private bool _isPanning;

    /// <summary>
    /// 自分に合わせるのをやめた後に最初に見た自分の位置。自分の位置(x と z)がこれから変わったら自分に合わせ直す。
    /// null は、次の更新で今の位置を控える(やめた直後と、掴み終わった直後)。
    /// </summary>
    private EntityPositionStore.WorldPosition? _manualSelfPosition;

    /// <summary>
    /// 自分に合わせていないときに見たい中心(ワールド座標の x と z)。別のマップで位置の分からない人を選んだときは、最初に地図の中央を入れる。
    /// 出すときに地図の外が見えない位置へ寄せるが、寄せた値は書き戻さない(表示範囲を狭くすれば、見たい中心へ戻る)。
    /// </summary>
    private double _centerX;
    private double _centerZ;
    private bool _hasCenter;

    /// <summary>
    /// 前回の更新で出した地図・表示範囲・倍率(画面の要素の長さ ÷ ワールド座標の長さ)・寄せた後の見る中心。
    /// ホイールと掴む操作の基準にする。倍率 0 は、まだ何も出していない。
    /// </summary>
    private SceneMapDefinition? _shownMap;
    private double _shownViewRange;
    private double _shownScale;
    private double _shownCenterX;
    private double _shownCenterZ;

    private MapImageCache.MapImageLease? _imageLease;
    private string? _imagePath;

    /// <summary>地図の表に画像があるのに、アプリの画像のフォルダにファイルが無い。</summary>
    private bool _isImageFileMissing;

    /// <summary>前回の更新で読んだ名簿と周りの実体の一覧。どちらも変わっていなければ、ある印の名前とアイコンは引き直さない。</summary>
    private IReadOnlyList<PlayerRosterEntry>? _lastRoster;
    private IReadOnlyList<NearbyEntityEntry>? _lastEntities;

    /// <summary>設定・言語が変わった。次の更新で印の名前とアイコンを全部引き直す。</summary>
    private bool _isPresentationStale = true;

    private bool _isDisposed;

    /// <summary>
    /// フッターの左。映しているマップの名前とチャンネル(プレイヤーリストの左下と同じ形)。まだ分からなければ空。
    /// フッターの画面はプレイヤーリスト・エンティティリストと同じ(<c>ListWidgetFooterView</c>)で、欄の名前もそろえる。
    /// </summary>
    [ObservableProperty]
    private string _mapName = string.Empty;

    /// <summary>フッターの右の1組目の見出し。個数なら「エンティティ:」、得点なら「交戦中:」。</summary>
    [ObservableProperty]
    private string _footerCombatLabel = string.Empty;

    /// <summary>フッターの右の1組目の値。個数ならエンティティの数、得点なら交戦中の得点。</summary>
    [ObservableProperty]
    private FooterValue _footerCombatValue = FooterValue.Empty;

    /// <summary>フッターの右の2組目の見出し。個数なら「プレイヤー:」、得点なら「現在:」。</summary>
    [ObservableProperty]
    private string _footerOverallLabel = string.Empty;

    /// <summary>フッターの右の2組目の値。個数ならプレイヤーの数、得点なら今までに得た得点。</summary>
    [ObservableProperty]
    private FooterValue _footerOverallValue = FooterValue.Empty;

    /// <summary>地図の画像。読み終わるまでと、画像の無いマップは null。</summary>
    [ObservableProperty]
    private BitmapSource? _mapImage;

    /// <summary>地図の画像の画素の幅と高さ。画像の要素はこの大きさで置き、<see cref="MapImageMatrix"/> で層の大きさへ伸ばす。</summary>
    [ObservableProperty]
    private double _mapImagePixelWidth;

    [ObservableProperty]
    private double _mapImagePixelHeight;

    /// <summary>地図の画像の画素から層の位置への拡大(層の原点は地図の左上)。</summary>
    [ObservableProperty]
    private Matrix _mapImageMatrix = Matrix.Identity;

    /// <summary>
    /// 地図の濃さ(0〜1)。設定「マップの不透明度」。窓の不透明度がどの値でもこの濃さで見えるよう、
    /// 画面が枠の層の画像と印の層の画像に分けて掛ける。印には掛けない。
    /// </summary>
    [ObservableProperty]
    private double _mapImageOpacity;

    /// <summary>地図の画像の層と印の層を窓の中へずらす量。</summary>
    [ObservableProperty]
    private double _layerOffsetX;

    [ObservableProperty]
    private double _layerOffsetY;

    /// <summary>別のマップを見ているか。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReturnToCurrentMap))]
    [NotifyPropertyChangedFor(nameof(IsContextMenuEnabled))]
    private bool _isViewingOtherMap;

    /// <summary>
    /// チャットの座標のリンクで選んだ、自分とは別の場所(別のマップか、自分のいるマップの別の階層)を見ているか(<see cref="ShowSharedPosition"/>)。
    /// この間は見る中心と階層をリンクのものにし、自分が動いても・地図を掴んで動かしても・ダブルクリックしても戻さない。
    /// 抜けるのは「自分のいるマップに戻る」と自分のマップ移動、自分がその階層へ移動したとき、プレイヤー情報の現在地から別の人を選んだとき
    /// (「位置のマークを消す」は印を消すだけで抜けない)。自分のいるマップの自分の階層のリンクでは入らない。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReturnToCurrentMap))]
    [NotifyPropertyChangedFor(nameof(IsContextMenuEnabled))]
    private bool _isViewingSharedPosition;

    /// <summary>共有された位置を持っているか。右クリックのメニュー「位置のマークを消す」はこの間だけ出す。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsContextMenuEnabled))]
    private bool _hasSharedPosition;

    /// <param name="requestPlayerWindow">プレイヤーの窓を開く(種別とキャラID)。</param>
    /// <param name="showEntity">ターゲット情報で敵を映す。</param>
    public MapWidgetViewModel(
        WidgetListItemViewModel widget,
        Action<WidgetKind, long> requestPlayerWindow,
        Action<EntityTargetInfo> showEntity)
    {
        _widget = widget;
        _requestPlayerWindow = requestPlayerWindow;
        _showEntity = showEntity;
        _settings = widget.GetMapSettingsSnapshot();
        _mapImageOpacity = ToOpacity(_settings);
        _selfSceneId = CurrentSceneId;
        Markers = new ReadOnlyObservableCollection<MapMarkerEntry>(_markers);

        _widget.MapSettingsChanged += Widget_MapSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;
    }

    /// <summary>自分のいるマップの番号。まだ分からなければ 0。</summary>
    public static int CurrentSceneId => EncounterManager.LevelMapId is > 0 and <= int.MaxValue
        ? (int)EncounterManager.LevelMapId
        : 0;

    /// <summary>いま映しているマップの番号。自分のいるマップを見ていて、まだ分からなければ 0。</summary>
    public int SceneId => IsViewingOtherMap ? _viewedSceneId : CurrentSceneId;

    /// <summary>
    /// ヘッダーのボタンと右クリックのメニュー「自分のいるマップに戻る」を出すか。
    /// 別のマップを見ている間と、リンクで選んだ場所を見ている間。
    /// </summary>
    public bool CanReturnToCurrentMap => IsViewingOtherMap || IsViewingSharedPosition;

    /// <summary>右クリックのメニューを開けるか。出せる項目が1つも無ければ開かない。</summary>
    public bool IsContextMenuEnabled => CanReturnToCurrentMap || HasSharedPosition;

    /// <summary>窓のカード。右クリックのメニューの色を窓のパレットから取る。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public ReadOnlyObservableCollection<MapMarkerEntry> Markers { get; }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>
    /// 地図を掴んで動かせるか。何かを出していて、地図が領域からはみ出している向きがあるとき
    /// (地図の全体が入っていれば、動かす所が無い)。地図の無いマップは範囲が無いので、どの向きにも動かせる。
    /// </summary>
    public bool CanPan => _shownScale > 0d
        && (_shownMap is not { } map
            || map.Width * _shownScale > _viewportWidth + PanSlack
            || map.Depth * _shownScale > _viewportHeight + PanSlack);

    public void RefreshDataStatus()
    {
        var status = WidgetDataStatusRules.ForMap(
            GameReceptionStateStore.Current,
            !IsViewingOtherMap,
            _isImageFileMissing || _imageLease?.HasFailed == true);
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>地図を出す領域の大きさと、印が端からはみ出して見える幅(どちらも画面の要素から)。変わったら当て直す。</summary>
    public void SetViewport(double width, double height, double markerOverflow)
    {
        if (_viewportWidth == width
            && _viewportHeight == height
            && _markerOverflow == markerOverflow)
        {
            return;
        }

        _viewportWidth = width;
        _viewportHeight = height;
        _markerOverflow = markerOverflow;
        Refresh();
    }

    /// <summary>
    /// 窓の上のホイール。回した量に比例して表示範囲を変える(1刻みで <see cref="WheelZoomFactor"/> 倍。奥へ回すと狭く、手前へ回すと広く)。
    /// 見る中心は変えず、その地図で出せる範囲(<see cref="GetViewRangeLimits"/>)の端で止まる。値は窓ごとで、保存しない。
    /// </summary>
    /// <param name="notches">回した量(1刻みが 1。奥へ回すと正)。</param>
    /// <returns>ホイールを受けたか。まだ何も出していなければ受けない。</returns>
    public bool ZoomByWheel(double notches)
    {
        if (_shownScale <= 0d)
        {
            return false;
        }

        if (notches == 0d)
        {
            return true;
        }

        var (narrowest, widest) = GetViewRangeLimits(_shownMap);
        var viewRange = Math.Clamp(_shownViewRange * Math.Pow(WheelZoomFactor, -notches), narrowest, widest);
        if (viewRange == _shownViewRange)
        {
            return true;
        }

        _viewRange = viewRange;
        Refresh();
        return true;
    }

    /// <summary>地図を掴み始めた(押したまま、Windows のドラッグの距離より動いた)。離すまで自分に合わせ直さない。</summary>
    public void BeginPan()
    {
        _isPanning = true;
    }

    /// <summary>
    /// 地図を掴んで、領域の中で動かした(画面のコードがマウスの動いた量を渡す)。見たい中心をその分ずらし、自分に合わせるのをやめる。
    /// 地図の外は見せないので、地図がはみ出していない向きには動かない。
    /// </summary>
    public void PanBy(double deltaX, double deltaY)
    {
        if (!CanPan || (deltaX == 0d && deltaY == 0d))
        {
            return;
        }

        // 地図を右へ動かすと見る中心は左(x の小さい側)へ、下へ動かすと上(z の大きい側)へ動く。
        _isFollowingSelf = false;
        _centerX = _shownCenterX - deltaX / _shownScale;
        _centerZ = _shownCenterZ + deltaY / _shownScale;
        _hasCenter = true;
        Refresh();
    }

    /// <summary>地図を離した。ここから自分が動いたら自分に合わせ直す(離したときの位置を次の更新で控える)。</summary>
    public void EndPan()
    {
        _isPanning = false;
        _manualSelfPosition = null;
    }

    /// <summary>
    /// 見る中心を自分に合わせ直す(地図を出す領域のダブルクリック)。自分のいるマップを見ている間だけで、
    /// リンクで選んだ場所を見ている間は何もしない(戻るのは「自分のいるマップに戻る」かマップ移動)。
    /// </summary>
    public void ResumeFollowingSelf()
    {
        if (IsViewingOtherMap || IsViewingSharedPosition || _isFollowingSelf)
        {
            return;
        }

        _isFollowingSelf = true;
        Refresh();
    }

    /// <summary>
    /// 印を左クリックした。プレイヤーの印はその人のプレイヤー情報を開き(もう開いていれば前に出す)、
    /// 敵の印はターゲット情報でその敵を映す(エンティティリストの行の左クリックと同じ)。敵が周りの実体の一覧から外れていれば何もしない。
    /// </summary>
    public void OpenMarkerWindow(MapMarkerEntry marker)
    {
        if (marker.IsPlayer)
        {
            _requestPlayerWindow(WidgetKind.PlayerInfo, marker.Key.Id);
            return;
        }

        var entities = NearbyEntityStore.Instance.Current.Entries;
        for (var index = 0; index < entities.Count; index++)
        {
            if (entities[index].EntityUuid == marker.Key.Id)
            {
                _showEntity(EntityListEntry.CreateTarget(entities[index]));
                return;
            }
        }
    }

    /// <summary>
    /// プレイヤー情報の現在地から、その人を中心に出す。
    /// 自分なら自分に合わせ直す。同じマップの人はその位置を中心にし、自分が動いたら自分に合わせ直す(掴んで動かしたときと同じ)。
    /// ほかのマップにいるパーティの人はそのマップを出す(位置がまだ届いていなければ地図の中央)。
    /// 位置とマップは、周りにいれば周りの位置、そうでなければパーティの同期の位置、それも無ければパーティの情報のいる場所。
    /// どれも分からなければ何もしない。表示範囲は今の値のまま。
    /// </summary>
    public void ShowPlayer(long characterId)
    {
        if (characterId == 0)
        {
            return;
        }

        if (characterId == AppState.PlayerUID)
        {
            ShowCurrentMap();
            return;
        }

        var row = FindLivePlayer(characterId);
        var currentSceneId = CurrentSceneId;
        int sceneId;
        EntityPositionStore.WorldPosition? position = null;
        if (row is { IsNearby: true } && _positions.TryGetPosition(ToPlayerUuid(characterId), out var nearbyPosition))
        {
            sceneId = currentSceneId;
            position = nearbyPosition;
        }
        else if (_positions.TryGetPartyMemberLocation(characterId, out var location) && location.SceneId > 0)
        {
            sceneId = location.SceneId;
            position = location.Position;
        }
        else if (row is { PartySceneId: > 0 })
        {
            sceneId = row.PartySceneId;
        }
        else
        {
            return;
        }

        // 別の人を選んだら、リンクで選んだ場所を見るのはやめる(印は残す)。
        IsViewingSharedPosition = false;
        if (sceneId == currentSceneId)
        {
            _viewedSceneId = 0;
            _viewedLineId = 0;
            IsViewingOtherMap = false;
            if (position is { } shown)
            {
                SetManualCenter(shown);
            }
            else
            {
                _isFollowingSelf = true;
            }
        }
        else
        {
            _viewedSceneId = sceneId;
            _viewedLineId = (uint)Math.Max(row?.PartyLineId ?? 0, 0);
            IsViewingOtherMap = true;
            _isFollowingSelf = false;
            if (position is { } shown)
            {
                SetManualCenter(shown);
            }
            else
            {
                _hasCenter = false;
            }
        }

        _isPresentationStale = true;
        Refresh();
    }

    /// <summary>
    /// チャットの座標のリンクを押した。その場所を見る中心にし、リンクの階層の地図を出して印を置く。
    /// 自分のいるマップの場所なら自分のいるマップのまま、ほかのマップならそのマップを出す(<see cref="ShowPlayer"/> と同じ)。
    /// 別のマップか別の階層なら見ている状態(<see cref="IsViewingSharedPosition"/>)になる。
    /// </summary>
    public void ShowSharedPosition(MapSharedPosition position)
    {
        _sharedPosition = position;
        HasSharedPosition = true;
        _sharedPositionViewLayer = position.Layer;

        // 自分のいるマップの自分の階層なら、見ている状態には入らない(中心をその場所にするだけで、地図を掴んだ後と同じ)。
        var currentSceneId = CurrentSceneId;
        IsViewingSharedPosition = position.SceneId != currentSceneId || position.Layer != GetSelfLayer(AppState.PlayerUUID);
        if (position.SceneId == currentSceneId)
        {
            _viewedSceneId = 0;
            _viewedLineId = 0;
            IsViewingOtherMap = false;
        }
        else
        {
            _viewedSceneId = position.SceneId;
            _viewedLineId = position.LineId;
            IsViewingOtherMap = true;
        }

        SetManualCenter(new EntityPositionStore.WorldPosition(position.X, 0f, position.Z));
        _isPresentationStale = true;
        Refresh();
    }

    /// <summary>共有された位置の印を消す(右クリックのメニュー)。</summary>
    [RelayCommand]
    private void ClearSharedPosition()
    {
        if (ReleaseSharedPosition())
        {
            Refresh();
        }
    }

    /// <summary>共有された位置を捨てる。持っていなければ何もしない。</summary>
    /// <returns>捨てたか(呼び出し側が当て直すかを決める)。</returns>
    private bool ReleaseSharedPosition()
    {
        if (_sharedPosition is null)
        {
            return false;
        }

        _sharedPosition = null;
        HasSharedPosition = false;
        return true;
    }

    /// <summary>
    /// 自分のいるマップに戻り、自分と自分の階層に合わせ直す(別のマップを見ている間と、リンクで選んだ場所を見ている間の、
    /// ヘッダーのボタンと右クリックのメニュー)。印は消さない。
    /// </summary>
    [RelayCommand]
    private void ReturnToCurrentMap()
    {
        ShowCurrentMap();
    }

    private void ShowCurrentMap()
    {
        ResetToCurrentMapView();
        Refresh();
    }

    /// <summary>自分のいるマップを、自分を中心に自分の階層で見る形に戻す。当て直しは呼び手がする。</summary>
    private void ResetToCurrentMapView()
    {
        _viewedSceneId = 0;
        _viewedLineId = 0;
        IsViewingOtherMap = false;
        IsViewingSharedPosition = false;
        _isFollowingSelf = true;
        _isPresentationStale = true;
    }

    /// <summary>自分に合わせるのをやめ、その位置を見たい中心にする。自分の位置は次の更新で控える。</summary>
    private void SetManualCenter(EntityPositionStore.WorldPosition position)
    {
        _isFollowingSelf = false;
        _manualSelfPosition = null;
        _centerX = position.X;
        _centerZ = position.Z;
        _hasCenter = true;
    }

    /// <summary>名簿のその人の行(記録から来た行は除く)。居なければ null。</summary>
    private static PlayerRosterEntry? FindLivePlayer(long characterId)
    {
        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        for (var index = 0; index < roster.Count; index++)
        {
            var row = roster[index];
            if (row.CharacterId == characterId && !row.IsFromRecord)
            {
                return row;
            }
        }

        return null;
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _widget.MapSettingsChanged -= Widget_MapSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
        ReleaseImage();
    }

    /// <summary>
    /// 位置の控えを読んで、地図と印を当て直す。画面が出ている間、画面のコードが定期に呼ぶ。
    /// </summary>
    public void Refresh()
    {
        if (_isDisposed)
        {
            return;
        }

        // ログアウトしたら、共有された位置も捨てる(入り直したキャラには別の場所)。
        if (AppState.PlayerUID == 0)
        {
            ReleaseSharedPosition();
        }

        var currentSceneId = CurrentSceneId;
        if (currentSceneId != _selfSceneId)
        {
            // 自分がマップを移動した。自分のいるマップを見ていれば、移動先では自分に合わせ直す。
            // リンクで選んだ場所を見ていたら、自分のいるマップと自分と自分の階層に戻す(印は消さない)。
            _selfSceneId = currentSceneId;
            if (IsViewingSharedPosition)
            {
                ResetToCurrentMapView();
            }
            else if (!IsViewingOtherMap)
            {
                _isFollowingSelf = true;
            }
        }

        // 見ていた別のマップへ自分が移動したら、自分のいるマップとして自分に合わせる。
        if (IsViewingOtherMap && _viewedSceneId == currentSceneId)
        {
            _viewedSceneId = 0;
            _viewedLineId = 0;
            IsViewingOtherMap = false;
            _isFollowingSelf = true;
        }

        var sceneId = SceneId;
        RefreshFooterMapName(sceneId);

        if (_viewportWidth <= 0d || _viewportHeight <= 0d)
        {
            return;
        }

        var map = sceneId > 0 ? CombatDataCatalog.GetSceneMap(sceneId) : null;

        if (IsViewingOtherMap)
        {
            RefreshOtherMap(sceneId, map);
        }
        else
        {
            RefreshCurrentMap(sceneId, map);
        }

        RefreshDataStatus();
    }

    /// <summary>自分のいるマップ。見る中心は自分(地図を掴んで動かした後と現在地から選んだ後は、自分が動くまでその場所)。</summary>
    private void RefreshCurrentMap(int sceneId, SceneMapDefinition? map)
    {
        // 得点はエンティティリストと同じ控えと決まり(得点を求めている間だけ、印とフッター)。
        var dungeonScore = DungeonScoreStore.Instance.Current;
        var isScoreRequested = dungeonScore.IsScoreRequested;
        var entities = NearbyEntityStore.Instance.Current.Entries;
        var selfUuid = AppState.PlayerUUID;
        if (selfUuid == 0 || !_positions.TryGetPosition(selfUuid, out var selfPosition))
        {
            Clear();
            RenderCurrentMapFooter(dungeonScore, entities, entityCount: 0, playerCount: 0);
            return;
        }

        // リンクで選んだ別の階層を見ている間に、自分がその階層へ移動したら、見ている状態を抜ける。
        // 中心はそのままで、ここからは地図を掴んだ後と同じ(層は自分に付いていき、自分が動いたら自分に合わせ直す)。
        var selfLayer = GetSelfLayer(selfUuid);
        if (IsViewingSharedPosition && selfLayer == _sharedPositionViewLayer)
        {
            IsViewingSharedPosition = false;
            _manualSelfPosition = null;
        }

        // 自分に合わせるのをやめた後、自分が動いたら(水平の位置が変わったら)合わせ直す。
        // 掴んでいる間と、リンクで選んだ場所を見ている間は合わせ直さない。
        if (!_isFollowingSelf && !_isPanning && !IsViewingSharedPosition)
        {
            if (_manualSelfPosition is not { } heldPosition)
            {
                _manualSelfPosition = selfPosition;
            }
            else if (heldPosition.X != selfPosition.X || heldPosition.Z != selfPosition.Z)
            {
                _isFollowingSelf = true;
            }
        }

        ApplyImage(map, IsViewingSharedPosition ? _sharedPositionViewLayer : selfLayer);

        var (originX, originZ, scale) = ApplyView(
            map,
            _isFollowingSelf ? selfPosition.X : _centerX,
            _isFollowingSelf ? selfPosition.Z : _centerZ);

        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        var refreshesPresentation = BeginMarkers(roster, entities, isScoreRequested);
        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        var party = PartyStateStore.Instance.Current;
        var partyDisplayMode = (PartyDisplayMode)_settings.PartyDisplayModeIndex;
        var entityDisplayMode = (EntityDisplayMode)_settings.EntityDisplayModeIndex;
        var playerNameMode = (PartyDisplayMode)_settings.PlayerNamePartyDisplayModeIndex;
        var showsEntityNames = _settings.EntityNameDisplayModeIndex == WidgetConfigDefaults.MapEntityNameShownIndex;

        // フッターの個数は、このマップに置く相手のうちフィルターを通った全部(表示範囲の外も数える。ズームで数が変わらないように)。
        var entityCount = 0;
        var playerCount = 0;
        for (var index = 0; index < entities.Count; index++)
        {
            var entity = entities[index];
            if (!EntityListEntry.IsShown(entity, entityDisplayMode) || !_positions.TryGetPosition(entity.EntityUuid, out var entityPosition))
            {
                continue;
            }

            entityCount++;
            if (TryPlaceMarker(
                    new MapMarkerKey(MapMarkerKind.Entity, entity.EntityUuid),
                    entityPosition,
                    originX,
                    originZ,
                    scale,
                    zIndex: 0,
                    out var marker,
                    out var isNew)
                && (isNew || refreshesPresentation))
            {
                ApplyEntityPresentation(marker, entity, isScoreRequested, showsEntityNames);
            }
        }

        var hasSelfRow = false;
        for (var index = 0; index < roster.Count; index++)
        {
            var row = roster[index];
            if (row.IsFromRecord)
            {
                continue;
            }

            if (row.IsSelf)
            {
                hasSelfRow = true;
            }

            if (!party.ShouldInclude(row.CharacterId, row.IsSelf, partyDisplayMode))
            {
                continue;
            }

            EntityPositionStore.WorldPosition position;
            if (row.IsSelf)
            {
                position = selfPosition;
            }
            else if (row.IsNearby && _positions.TryGetPosition(ToPlayerUuid(row.CharacterId), out var nearbyPosition))
            {
                position = nearbyPosition;
            }
            else if (!TryGetPartyMemberPosition(row, sceneId, out position))
            {
                continue;
            }

            playerCount++;
            if (!TryPlaceMarker(
                    new MapMarkerKey(MapMarkerKind.Player, row.CharacterId),
                    position,
                    originX,
                    originZ,
                    scale,
                    GetPlayerZIndex(row),
                    out var marker,
                    out var isNew))
            {
                continue;
            }

            if (isNew || refreshesPresentation)
            {
                ApplyPlayerPresentation(
                    marker,
                    row,
                    nameDisplayMode,
                    party.ShouldInclude(row.CharacterId, row.IsSelf, playerNameMode));
            }

            if (row.IsSelf)
            {
                ApplySelfFacing(marker, selfUuid);
            }
        }

        // 名簿に自分の行がまだ無くても、自分の位置は出す(名前は UID、アイコンは不明)。フィルターは自分の行と同じ。
        var showsSelfWithoutRow = !hasSelfRow && party.ShouldInclude(AppState.PlayerUID, true, partyDisplayMode);
        if (showsSelfWithoutRow)
        {
            playerCount++;
        }

        if (showsSelfWithoutRow
            && TryPlaceMarker(
                new MapMarkerKey(MapMarkerKind.Player, AppState.PlayerUID),
                selfPosition,
                originX,
                originZ,
                scale,
                zIndex: 3,
                out var selfMarker,
                out var isSelfMarkerNew))
        {
            if (isSelfMarkerNew || refreshesPresentation)
            {
                // 名簿の行が無い間は戦闘状態が分からないので、名前はそのまま。簡略化するのは表示設定「自分を常に簡略化表示」のときだけ。
                selfMarker.Name = PlayerInfoFormatFormatter.GetDisplayName(null, AppState.PlayerUID, true, false, 0, nameDisplayMode);
                selfMarker.ShowsName = party.ShouldInclude(AppState.PlayerUID, true, playerNameMode);
                selfMarker.Shape = _settings.AlwaysSimplifiesSelf
                    ? GetSimplifiedPlayerShape(isSelf: true)
                    : MapMarkerShape.Icon;
                ApplyIcon(selfMarker, PlayerProfession.GetKey(0));
            }

            ApplySelfFacing(selfMarker, selfUuid);
        }

        PlaceFieldMarkMarkers(originX, originZ, scale);
        PlaceSharedPositionMarker(sceneId, originX, originZ, scale);
        EndMarkers();
        RenderCurrentMapFooter(dungeonScore, entities, entityCount, playerCount);
    }

    /// <summary>
    /// 自分のいるマップのフッターの右。得点を求めている間はエンティティリストと同じ得点(交戦中は周りの戦闘状態の敵の得点の合計、
    /// 現在は今までに得た得点)、それ以外はこのマップに置く相手の個数。
    /// </summary>
    private void RenderCurrentMapFooter(
        DungeonScoreSnapshot dungeonScore,
        IReadOnlyList<NearbyEntityEntry> entities,
        int entityCount,
        int playerCount)
    {
        if (dungeonScore.IsScoreRequested)
        {
            RenderFooterValues(isScoreShown: true, EntityListEntry.GetCombatScore(entities), dungeonScore.TotalScore);
            return;
        }

        RenderFooterValues(isScoreShown: false, entityCount, playerCount);
    }

    /// <summary>
    /// 別のマップ。見る中心は選んだ人の位置(位置が分からなければ地図の中央。地図を掴んで動かした後は、その場所)。
    /// 地図の無いマップは、選んだ人の位置を中心に印だけを出す(位置も分からなければ何も出さない)。
    /// </summary>
    private void RefreshOtherMap(int sceneId, SceneMapDefinition? map)
    {
        // 別のマップには実体が届かないので、フッターはいつも個数(エンティティは 0、プレイヤーはそのマップのパーティの人のうちフィルターを通った人)。
        if (map is null && !_hasCenter)
        {
            Clear();
            RenderFooterValues(isScoreShown: false, 0, 0);
            return;
        }

        ApplyImage(map, IsViewingSharedPosition ? _sharedPositionViewLayer : 0);

        if (!_hasCenter && map is not null)
        {
            _centerX = map.X0 + map.Width / 2d;
            _centerZ = map.Z0 + map.Depth / 2d;
            _hasCenter = true;
        }

        var (originX, originZ, scale) = ApplyView(map, _centerX, _centerZ);

        var roster = PlayerRosterPresentationStore.Instance.Current.Entries;
        var refreshesPresentation = BeginMarkers(roster, entities: null, isScoreRequested: false);
        var nameDisplayMode = PlayerRosterPresentationStore.Instance.NameDisplayMode;
        var party = PartyStateStore.Instance.Current;
        var partyDisplayMode = (PartyDisplayMode)_settings.PartyDisplayModeIndex;
        var playerNameMode = (PartyDisplayMode)_settings.PlayerNamePartyDisplayModeIndex;

        var playerCount = 0;
        for (var index = 0; index < roster.Count; index++)
        {
            var row = roster[index];
            if (row.IsFromRecord
                || !party.ShouldInclude(row.CharacterId, row.IsSelf, partyDisplayMode)
                || !TryGetPartyMemberPosition(row, sceneId, out var position))
            {
                continue;
            }

            playerCount++;
            if (TryPlaceMarker(
                    new MapMarkerKey(MapMarkerKind.Player, row.CharacterId),
                    position,
                    originX,
                    originZ,
                    scale,
                    GetPlayerZIndex(row),
                    out var marker,
                    out var isNew)
                && (isNew || refreshesPresentation))
            {
                ApplyPlayerPresentation(
                    marker,
                    row,
                    nameDisplayMode,
                    party.ShouldInclude(row.CharacterId, row.IsSelf, playerNameMode));
            }
        }

        PlaceSharedPositionMarker(sceneId, originX, originZ, scale);
        EndMarkers();
        RenderFooterValues(isScoreShown: false, 0, playerCount);
    }

    /// <summary>
    /// 表示範囲と見たい中心から、倍率・画像の拡大・層をずらす量を決める。見る中心は地図の外が見えない位置へ寄せる。
    /// 層の原点は、地図があれば地図の左上、無ければワールド座標の原点。
    /// </summary>
    /// <returns>層の原点(ワールド座標の x と z)と倍率。</returns>
    private (double OriginX, double OriginZ, double Scale) ApplyView(SceneMapDefinition? map, double centerX, double centerZ)
    {
        var (narrowest, widest) = GetViewRangeLimits(map);
        var viewRange = Math.Clamp(_viewRange, narrowest, widest);
        var scale = Math.Min(_viewportWidth, _viewportHeight) / viewRange;
        if (map is not null)
        {
            centerX = ClampCenter(centerX, map.X0, map.Width, _viewportWidth / scale);
            centerZ = ClampCenter(centerZ, map.Z0, map.Depth, _viewportHeight / scale);
        }

        var originX = map?.X0 ?? 0d;
        var originZ = map is null ? 0d : map.Z0 + map.Depth;
        ApplyImageMatrix(map, scale);
        LayerOffsetX = Math.Round(_viewportWidth / 2d - (centerX - originX) * scale);
        LayerOffsetY = Math.Round(_viewportHeight / 2d - (originZ - centerZ) * scale);

        _shownMap = map;
        _shownViewRange = viewRange;
        _shownScale = scale;
        _shownCenterX = centerX;
        _shownCenterZ = centerZ;
        return (originX, originZ, scale);
    }

    /// <summary>
    /// その地図で出せる表示範囲の両端。
    /// いちばん広いのは、地図の長い辺が領域の短い辺に入るところ(地図の全体が入る)。
    /// いちばん狭いのは <see cref="NarrowestViewRange"/>(地図の画像の細かさは見ない。どの地図も同じところまで拡大でき、画像は引き伸ばす)。
    /// 全体がそれより狭い地図は、全体だけを出す。地図の無いマップは決まった両端。
    /// </summary>
    private (double Narrowest, double Widest) GetViewRangeLimits(SceneMapDefinition? map)
    {
        if (map is null)
        {
            return (NarrowestViewRange, WidestViewRangeWithoutMap);
        }

        var widest = Math.Max(map.Width, map.Depth);
        return (Math.Min(NarrowestViewRange, widest), widest);
    }

    /// <summary>見る中心を、その向きに地図の外が見えない位置へ寄せる。地図が領域より大きくない向きは地図の中央。</summary>
    private static double ClampCenter(double center, double mapStart, double mapLength, double visibleLength)
    {
        return mapLength <= visibleLength
            ? mapStart + mapLength / 2d
            : Math.Clamp(center, mapStart + visibleLength / 2d, mapStart + mapLength - visibleLength / 2d);
    }

    /// <summary>パーティの人(自分以外)が、パーティの同期でそのマップにいると届いていれば、その位置。</summary>
    private bool TryGetPartyMemberPosition(PlayerRosterEntry row, int sceneId, out EntityPositionStore.WorldPosition position)
    {
        if (row.IsPartyMember
            && !row.IsSelf
            && _positions.TryGetPartyMemberLocation(row.CharacterId, out var location)
            && location.SceneId == sceneId)
        {
            position = location.Position;
            return true;
        }

        position = default;
        return false;
    }

    /// <summary>自分のいる階層(自分のエリアの階層。エリアがまだ届いていなければ 0)。</summary>
    private int GetSelfLayer(long selfUuid)
    {
        return CombatDataCatalog.GetSceneAreaLayer(_positions.GetSceneAreaId(selfUuid));
    }

    private static long ToPlayerUuid(long characterId)
    {
        return Utils.EntityIdToUuid(characterId, (long)EEntityType.EntChar, isSummon: false, isClient: false);
    }

    /// <summary>
    /// 重なりの順(<see cref="MapMarkerEntry.ZIndex"/>)。フィールドマーク -1(<see cref="PlaceFieldMarkMarkers"/>)→ 実体 0 →
    /// 周りのプレイヤー 1 → パーティの人 2 → 自分 3 → 共有された位置 4(<see cref="PlaceSharedPositionMarker"/>)。
    /// </summary>
    private static int GetPlayerZIndex(PlayerRosterEntry row)
    {
        return row.IsSelf ? 3 : row.IsPartyMember ? 2 : 1;
    }

    /// <summary>
    /// ダンジョンで隊長が置いたフィールドマーク(Core <see cref="FieldMarkStore"/>)。自分のいるマップを見ているときだけ置き、階層は問わない
    /// (マークに階層の情報が無い)。絵はゲームのマークの番号の絵で、色は変えない。名前も得点も出さず、押しても何もしない。
    /// 場に置く印なので、人と敵の印より奥に描く。
    /// </summary>
    private void PlaceFieldMarkMarkers(double originX, double originZ, double scale)
    {
        FieldMarkStore.Instance.CopyTo(_fieldMarks);
        foreach (var mark in _fieldMarks)
        {
            var placed = TryPlaceMarker(
                new MapMarkerKey(MapMarkerKind.FieldMark, mark.OwnerUuid * 8 + mark.Number),
                mark.Position,
                originX,
                originZ,
                scale,
                zIndex: -1,
                out var marker,
                out var isNew);
            if (placed && isNew)
            {
                marker.Shape = MapMarkerShape.FieldMark;
                marker.IconImage = (ImageSource)Application.Current.FindResource(FieldMarkIconKeyPrefix + mark.Number);
            }
        }
    }

    /// <summary>
    /// チャットで共有された位置の印。いま映しているマップの場所のときだけ置く。
    /// 絵はゲームが位置の追跡に出すものと同じで、色は変えない。名前も得点も出さず、押しても何もしない。
    /// </summary>
    private void PlaceSharedPositionMarker(int sceneId, double originX, double originZ, double scale)
    {
        if (_sharedPosition is not { } position || position.SceneId != sceneId)
        {
            return;
        }

        var placed = TryPlaceMarker(
            new MapMarkerKey(MapMarkerKind.SharedPosition, 0),
            new EntityPositionStore.WorldPosition(position.X, 0f, position.Z),
            originX,
            originZ,
            scale,
            zIndex: 4,
            out var marker,
            out var isNew);
        if (placed && isNew)
        {
            marker.Shape = MapMarkerShape.Image;
            marker.IconImage = (ImageSource)Application.Current.FindResource(SharedPositionIconKey);
        }
    }

    /// <summary>
    /// 印を当て始める。名簿か周りの実体の一覧か、今の場所が得点を求めているかが前回から変わったか、設定・言語が変わっていれば、
    /// ある印の名前・アイコン・得点も引き直す(戻り値が真)。
    /// </summary>
    private bool BeginMarkers(IReadOnlyList<PlayerRosterEntry> roster, IReadOnlyList<NearbyEntityEntry>? entities, bool isScoreRequested)
    {
        _placedKeys.Clear();
        var refreshesPresentation = _isPresentationStale
            || !ReferenceEquals(roster, _lastRoster)
            || !ReferenceEquals(entities, _lastEntities)
            || isScoreRequested != _lastIsScoreRequested;
        _lastRoster = roster;
        _lastEntities = entities;
        _lastIsScoreRequested = isScoreRequested;
        _isPresentationStale = false;
        return refreshesPresentation;
    }

    /// <summary>
    /// 相手の位置を層の位置にして印へ入れる。窓の外(端からはみ出して見える幅の外)なら印を作らず、あった印は <see cref="EndMarkers"/> で外れる。
    /// </summary>
    private bool TryPlaceMarker(
        MapMarkerKey key,
        EntityPositionStore.WorldPosition position,
        double originX,
        double originZ,
        double scale,
        int zIndex,
        out MapMarkerEntry marker,
        out bool isNew)
    {
        var x = Math.Round((position.X - originX) * scale);
        var y = Math.Round((originZ - position.Z) * scale);
        var viewX = x + LayerOffsetX;
        var viewY = y + LayerOffsetY;
        isNew = false;
        if (viewX < -_markerOverflow
            || viewX > _viewportWidth + _markerOverflow
            || viewY < -_markerOverflow
            || viewY > _viewportHeight + _markerOverflow
            || !_placedKeys.Add(key))
        {
            marker = null!;
            return false;
        }

        if (!_markersByKey.TryGetValue(key, out var existing))
        {
            existing = new MapMarkerEntry(key);
            _markersByKey.Add(key, existing);
            _markers.Add(existing);
            isNew = true;
        }

        marker = existing;
        marker.X = x;
        marker.Y = y;
        marker.ZIndex = zIndex;
        return true;
    }

    /// <summary>今回の更新で位置を当てなかった印を外す。</summary>
    private void EndMarkers()
    {
        if (_placedKeys.Count == _markers.Count)
        {
            return;
        }

        for (var index = _markers.Count - 1; index >= 0; index--)
        {
            var key = _markers[index].Key;
            if (!_placedKeys.Contains(key))
            {
                _markersByKey.Remove(key);
                _markers.RemoveAt(index);
            }
        }
    }

    /// <summary>何も出さない(自分の位置がまだ届いていない・地図の無いマップ)。</summary>
    private void Clear()
    {
        if (_markers.Count > 0)
        {
            _markers.Clear();
            _markersByKey.Clear();
        }

        _lastRoster = null;
        _lastEntities = null;
        _isImageFileMissing = false;
        _shownMap = null;
        _shownScale = 0d;
        ReleaseImage();
    }

    /// <param name="showsName">名前の欄を出すか(表示設定「プレイヤー名」)。出さない印もアイコンの TIPS に名前を出す。</param>
    private void ApplyPlayerPresentation(MapMarkerEntry marker, PlayerRosterEntry row, PlayerNameDisplayMode nameDisplayMode, bool showsName)
    {
        marker.Name = PlayerInfoFormatFormatter.GetDisplayName(
            row.Name,
            row.CharacterId,
            row.IsSelf,
            row.IsNpc,
            row.ProfessionId,
            nameDisplayMode);
        marker.ShowsName = showsName && !IsInCombatTarget(row.IsInCombat, _settings.HideNamesInCombatModeIndex, isPlayer: true);
        // 簡略化するのは、表示設定「自分を常に簡略化表示」のときの自分と、「交戦中のプレイヤーを簡略化表示」のときの戦闘状態のプレイヤー。
        // ほかはクラスアイコン。簡略化した形は GetSimplifiedPlayerShape で決める(自分の矢先の向きは ApplySelfFacing が毎回の更新で読む)。
        var simplifies = (row.IsSelf && _settings.AlwaysSimplifiesSelf)
            || (row.IsInCombat && _settings.SimplifiesPlayersInCombat);
        marker.Shape = simplifies
            ? GetSimplifiedPlayerShape(row.IsSelf)
            : MapMarkerShape.Icon;
        ApplyIcon(marker, PlayerProfession.GetKey(row.ProfessionId, row.ClassSpec));
    }

    /// <summary>
    /// 自分の簡略化した印の向きを当て直す。向きは位置と同じく動くたびに届くので、見た目の当て直しとは別に毎回読む。
    /// 向きが初めて届いたら、円から矢先に変わる(形の決まりは <see cref="GetSimplifiedPlayerShape"/>)。
    /// </summary>
    private void ApplySelfFacing(MapMarkerEntry marker, long selfUuid)
    {
        if (marker.Shape == MapMarkerShape.Icon)
        {
            return;
        }

        if (_positions.TryGetFacingDegrees(selfUuid, out var degrees))
        {
            marker.FacingDegrees = degrees;
        }

        marker.Shape = GetSimplifiedPlayerShape(isSelf: true);
    }

    /// <summary>
    /// 簡略化した人の印の形。自分は向きが届いていれば向きを指す矢先、まだ届いていなければほかの人と同じ円(0 度で出すと、向いていない北を指す)。
    /// 1回の更新の中で形を書き換え直すと、印が一瞬見えなくなって TIPS が閉じるので、形はここで決めた値だけを入れる。
    /// </summary>
    private MapMarkerShape GetSimplifiedPlayerShape(bool isSelf)
    {
        return isSelf && _positions.TryGetFacingDegrees(AppState.PlayerUUID, out _)
            ? MapMarkerShape.Arrow
            : MapMarkerShape.Circle;
    }

    /// <param name="showsName">名前の欄を出すか(表示設定「エンティティ名」)。出さない印もアイコンの TIPS に名前を出す。</param>
    private void ApplyEntityPresentation(MapMarkerEntry marker, NearbyEntityEntry entity, bool isScoreRequested, bool showsName)
    {
        marker.Name = EntityListEntry.ResolveName(entity);
        marker.ShowsName = showsName && !IsInCombatTarget(entity.IsInCombat, _settings.HideNamesInCombatModeIndex, isPlayer: false);
        // ボスだけアイコン(ボスの印の絵)で、ほかは戦闘状態にかかわらず小さい円。色は全部アイコンカラー「エンティティ」。
        var isBoss = EntityListEntry.IsBoss(entity);
        marker.Shape = isBoss
            ? MapMarkerShape.Icon
            : MapMarkerShape.Circle;
        // ボスの得点はエンティティリストのアイコンと同じ決まり(交戦中も出す)。
        // 円の得点は交戦していない間だけ出す(交戦中の得点はフッターの「交戦中」に入る)。
        marker.ScoreText = isBoss || !entity.IsInCombat
            ? EntityListEntry.FormatDisplayedScore(entity, isScoreRequested)
            : string.Empty;
        ApplyIcon(marker, WidgetConfigDefaults.MapEntityColorKey);
    }

    /// <summary>
    /// その相手が、表示設定「交戦中の名前非表示」の対象か。
    /// 戦闘状態が届かない相手(周りにいないパーティの人)はいつも対象外。
    /// </summary>
    private static bool IsInCombatTarget(bool isInCombat, int modeIndex, bool isPlayer)
    {
        if (!isInCombat)
        {
            return false;
        }

        return modeIndex switch
        {
            WidgetConfigDefaults.MapInCombatTargetAllIndex => true,
            WidgetConfigDefaults.MapInCombatTargetPlayersIndex => isPlayer,
            WidgetConfigDefaults.MapInCombatTargetEntitiesIndex => !isPlayer,
            _ => false
        };
    }

    /// <summary>アイコンの形と色。鍵はアイコンカラーの鍵(敵の種類かプレイヤーのクラス)。</summary>
    private void ApplyIcon(MapMarkerEntry marker, string iconKey)
    {
        marker.IconMask = GetIconMask(iconKey);
        marker.IconBrush = GetIconBrush(iconKey);
    }

    /// <summary>アイコンの形。絵は鍵ごとに1つなので使い回す。</summary>
    private ImageBrush GetIconMask(string iconKey)
    {
        if (!_iconMasks.TryGetValue(iconKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.Profession.{iconKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _iconMasks[iconKey] = mask;
        }

        return mask;
    }

    /// <summary>アイコンの色。設定が変われば <see cref="_iconBrushes"/> ごと捨てて引き直す。</summary>
    private SolidColorBrush GetIconBrush(string iconKey)
    {
        if (!_iconBrushes.TryGetValue(iconKey, out var brush))
        {
            brush = new SolidColorBrush(SelectIconColor(iconKey));
            brush.Freeze();
            _iconBrushes[iconKey] = brush;
        }

        return brush;
    }

    /// <summary>アイコンカラーで選んでいる色(引き方は蘇生リストのアイコンカラーと同じ)。設定は正規化済みで、全部の鍵を持つ。</summary>
    private Color SelectIconColor(string iconKey)
    {
        var fallback = Color.FromRgb(0xA8, 0xA8, 0xA8);
        if (!_settings.ClassColorPalettes.TryGetValue(iconKey, out var palette) || palette.Count == 0)
        {
            return fallback;
        }

        var selectedIndex = _settings.ClassColorIndexes.TryGetValue(iconKey, out var index)
            ? index
            : WidgetConfigDefaults.MinClassColorIndex;
        return ColorUtilities.TryParseHex(palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)], out var color)
            ? color
            : fallback;
    }

    /// <summary>
    /// その地図のその階層の画像を借りる。映す画像が前と同じなら何もしない。地図が無ければ手放す。
    /// 地図の表に画像があるのにファイルが無ければ、画像なしで続け、状態の印で読めないことを出す。
    /// </summary>
    private void ApplyImage(SceneMapDefinition? map, int layer)
    {
        if (map is null)
        {
            _isImageFileMissing = false;
            ReleaseImage();
            return;
        }

        var imageName = map.GetImageName(layer);
        var path = CombatIconResolver.ResolveMapImage(imageName);
        _isImageFileMissing = path is null;
        if (path is null)
        {
            if (LoggedMissingImageNames.Add(imageName))
            {
                Log.Error("Map image {ImageName} is missing from the image folder; the map is shown without its image", imageName);
            }

            ReleaseImage();
            return;
        }

        if (string.Equals(path, _imagePath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ReleaseImage();
        _imagePath = path;
        _imageLease = MapImageCache.Instance.Acquire(path);
        _imageLease.Completed += ImageLease_Completed;
        ApplyLoadedImage();
    }

    private void ReleaseImage()
    {
        if (_imageLease is not null)
        {
            _imageLease.Completed -= ImageLease_Completed;
            _imageLease.Dispose();
            _imageLease = null;
        }

        _imagePath = null;
        MapImage = null;
    }

    /// <summary>借りている画像が読み終わった(か、読めなかった)。</summary>
    private void ImageLease_Completed(object? sender, EventArgs e)
    {
        ApplyLoadedImage();
        Refresh();
    }

    private void ApplyLoadedImage()
    {
        var image = _imageLease?.Image;
        MapImage = image;
        if (image is not null)
        {
            MapImagePixelWidth = image.PixelWidth;
            MapImagePixelHeight = image.PixelHeight;
        }
    }

    /// <summary>画像の画素を、地図のワールド座標の幅 × 倍率の大きさへ伸ばす。</summary>
    private void ApplyImageMatrix(SceneMapDefinition? map, double scale)
    {
        if (map is null || MapImage is null || MapImagePixelWidth <= 0d || MapImagePixelHeight <= 0d)
        {
            return;
        }

        MapImageMatrix = new Matrix(
            map.Width * scale / MapImagePixelWidth,
            0d,
            0d,
            map.Depth * scale / MapImagePixelHeight,
            0d,
            0d);
    }

    /// <summary>
    /// フッターの左。自分のいるマップはプレイヤーリストの左下と同じ値(名簿のマップ名とチャンネル)、
    /// 別のマップはそのマップの名前と選んだ人のチャンネル(プレイヤー情報の現在地と同じ値)。元が同じなら作り直さない。
    /// </summary>
    private void RefreshFooterMapName(int sceneId)
    {
        if (IsViewingOtherMap)
        {
            if (!_isFooterStale && _footerMapSourceSceneId == sceneId && _footerMapSourceChannel == _viewedLineId)
            {
                return;
            }

            _footerMapSourceSceneId = sceneId;
            _footerMapSourceName = null;
            _footerMapSourceChannel = _viewedLineId;
            MapName = WidgetListItemViewModel.FormatMapName(CombatDataCatalog.GetSceneName(sceneId, 0), _viewedLineId);
            return;
        }

        var roster = PlayerRosterPresentationStore.Instance.Current;
        var name = roster.MapName ?? string.Empty;
        if (!_isFooterStale
            && _footerMapSourceSceneId == 0
            && string.Equals(name, _footerMapSourceName, StringComparison.Ordinal)
            && roster.MapChannel == _footerMapSourceChannel)
        {
            return;
        }

        _footerMapSourceSceneId = 0;
        _footerMapSourceName = name;
        _footerMapSourceChannel = roster.MapChannel;
        MapName = WidgetListItemViewModel.FormatMapName(name, roster.MapChannel);
    }

    /// <summary>
    /// フッターの右の2組。得点を出す間はエンティティリストと同じ値(交戦中の得点と今までに得た得点)、それ以外は個数。
    /// 値が前回と同じなら文字を作り直さない。
    /// </summary>
    /// <param name="firstValue">個数ならエンティティの数、得点なら交戦中の得点。</param>
    /// <param name="secondValue">個数ならプレイヤーの数、得点なら今までに得た得点(分からなければ null)。</param>
    private void RenderFooterValues(bool isScoreShown, int firstValue, int? secondValue)
    {
        if (!_isFooterStale
            && _isFooterScoreShown == isScoreShown
            && _footerFirstValue == firstValue
            && _footerSecondValue == secondValue)
        {
            return;
        }

        _isFooterStale = false;
        _footerFirstValue = firstValue;
        _footerSecondValue = secondValue;
        _isFooterScoreShown = isScoreShown;
        var localization = LocalizationManager.Instance;
        if (isScoreShown)
        {
            FooterCombatLabel = $"{localization.GetString("CombatState_Tip")}:";
            FooterCombatValue = EntityListEntry.GetScoreFooterValue(firstValue);
            FooterOverallLabel = $"{localization.GetString("EntityList_CurrentScore")}:";
            FooterOverallValue = EntityListEntry.GetScoreFooterValue(secondValue);
            return;
        }

        FooterCombatLabel = $"{localization.GetString("Map_FooterEntities")}:";
        FooterCombatValue = WidgetListItemViewModel.GetEntityCountFooterValue(firstValue);
        FooterOverallLabel = $"{localization.GetString("Map_FooterPlayers")}:";
        FooterOverallValue = WidgetListItemViewModel.GetPlayerCountFooterValue(secondValue ?? 0);
    }

    private static double ToOpacity(MapWidgetSettingsConfig settings)
    {
        return settings.MapOpacity / 100d;
    }

    /// <summary>アイコンカラーが同じか。設定は正規化済みで、どちらも全部の鍵を持つ。</summary>
    private static bool HasSameIconColors(MapWidgetSettingsConfig left, MapWidgetSettingsConfig right)
    {
        var keys = WidgetConfigDefaults.GetClassColorKeys(WidgetKind.Map);
        for (var index = 0; index < keys.Count; index++)
        {
            var key = keys[index];
            if (left.ClassColorIndexes[key] != right.ClassColorIndexes[key]
                || !left.ClassColorPalettes[key].SequenceEqual(right.ClassColorPalettes[key], StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 設定(保存かプレビュー)が変わった。不透明度を当て、フィルター・名前を出す相手・アイコンカラーが変わっていれば当て直す
    /// (アイコンカラーが変わっていれば色のブラシも作り直す)。
    /// </summary>
    private void Widget_MapSettingsChanged(object? sender, EventArgs e)
    {
        var settings = _widget.GetMapSettingsSnapshot();
        var hasSameIconColors = HasSameIconColors(_settings, settings);
        var hasSameFilters = _settings.PartyDisplayModeIndex == settings.PartyDisplayModeIndex
            && _settings.EntityDisplayModeIndex == settings.EntityDisplayModeIndex;
        var hasSamePresentation = _settings.PlayerNamePartyDisplayModeIndex == settings.PlayerNamePartyDisplayModeIndex
            && _settings.EntityNameDisplayModeIndex == settings.EntityNameDisplayModeIndex
            && _settings.HideNamesInCombatModeIndex == settings.HideNamesInCombatModeIndex
            && _settings.SimplifiesPlayersInCombat == settings.SimplifiesPlayersInCombat
            && _settings.AlwaysSimplifiesSelf == settings.AlwaysSimplifiesSelf;
        _settings = settings;
        MapImageOpacity = ToOpacity(settings);
        if (hasSameIconColors && hasSameFilters && hasSamePresentation)
        {
            return;
        }

        if (!hasSameIconColors)
        {
            _iconBrushes.Clear();
        }

        if (!hasSameIconColors || !hasSamePresentation)
        {
            _isPresentationStale = true;
        }

        Refresh();
    }

    /// <summary>全体設定(保存かプレビュー)が変わった。フッターのマップの名前の内部IDの注記と、印の名前を引き直す。</summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        _isFooterStale = true;
        _isPresentationStale = true;
        Refresh();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        _isFooterStale = true;
        _isPresentationStale = true;
        Refresh();
    }
}
