// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.App.Localization;

namespace StarResonanceDps.App.Models.Widgets;

public sealed partial class PlayerBuffEntry : ObservableObject
{
    /// <summary>
    /// 残り時間が分からないときにバッジへ出す文字。
    /// <b>アイコンに重ねる1文字の枠</b>なので記号1つ。4言語とも同じなのでリソースは持たない。
    /// </summary>
    private const string UnknownDurationBadgeText = "?";

    /// <summary>
    /// 一覧の列で数字の位置に差し込む文字。単位は <c>Widget_BuffDurationFormat</c> のものが付く。
    /// </summary>
    private const string UnknownDurationListText = "??";

    /// <summary>名前はバフ名のまま(カードの中身・プレイヤーリストのバッジ)。</summary>
    public PlayerBuffEntry(PlayerBuffSnapshot snapshot)
        : this(snapshot, snapshot.Name)
    {
    }

    /// <param name="name">行に出す名前(バフ・デバフ一覧は表示設定「バフ名」の書式を当てたもの)。</param>
    public PlayerBuffEntry(PlayerBuffSnapshot snapshot, string name)
    {
        Key = snapshot.Key;
        CardKey = snapshot.CardKey;
        Uuid = snapshot.Uuid;
        Update(snapshot, name);
    }

    private PlayerBuffEntry()
    {
        Key = string.Empty;
        CardKey = string.Empty;
    }

    /// <summary>
    /// 付いていないバフの行(バフ・デバフグループ)。絵と名前だけを出し、ゲージは出さず、時間の欄は <paramref name="inactiveText"/>。
    /// 付いたら <see cref="Update(PlayerBuffSnapshot, string)"/> で付いている行になる。
    /// </summary>
    public static PlayerBuffEntry CreateInactive(string name, string? iconName, string inactiveText)
    {
        var entry = new PlayerBuffEntry();
        entry.UpdateInactive(name, iconName, inactiveText);
        return entry;
    }

    /// <summary>行の鍵(バフの番号ごと、エンティティのリストでプレイヤーが付けたものは番号と付与したプレイヤーごと)。</summary>
    public string Key { get; private set; }

    /// <summary>この行から開くカードが追う鍵(バフの番号ごと)。</summary>
    public string CardKey { get; private set; }

    public long Uuid { get; private set; }

    /// <summary>バフのID。まとまり(料理・薬剤)の判定に要る。</summary>
    public int BaseId { get; private set; }

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _layerText = string.Empty;

    [ObservableProperty]
    private string _durationText = string.Empty;

    [ObservableProperty]
    private string _durationWithUnitText = string.Empty;

    [ObservableProperty]
    private string? _iconPath;

    /// <summary>
    /// 絵が Icons.xaml の絵(装備効果)なら、白く塗る面の型抜き(<see cref="CombatIconResolver.ResolveBuffIconMask"/>)。そうでなければ null。
    /// </summary>
    [ObservableProperty]
    private ImageBrush? _iconMask;

    /// <summary>絵がモジュール効果の絵か。モジュール効果の絵は丸く画像いっぱいに描かれているので、バフの絵と別の大きさで出す。</summary>
    [ObservableProperty]
    private bool _isModuleEffectIcon;

    /// <summary>絵が因子の絵か(<see cref="CombatIconResolver.IsFactorIcon"/>)。絵の位置を切り替える。</summary>
    [ObservableProperty]
    private bool _isFactorIcon;

    /// <summary>絵のパスはあるのにファイルが見つからないか。はてなを出す。パスが空なら何も出さない。</summary>
    [ObservableProperty]
    private bool _showsUnknownIcon;

    /// <summary>行のゲージの長さ(0〜1)。満タンになる残り時間は表示設定で決まる。</summary>
    [ObservableProperty]
    private double _barRatio = 1d;

    /// <summary>ゲージが満タンか。満タンのときだけ右端も角丸にする。</summary>
    [ObservableProperty]
    private bool _isBarFull = true;

    /// <summary>
    /// ゲージが 0 になるまでの秒数。表示側はこのぶんかけて線形に縮める。
    /// <b>0 は「今は減らない」</b>(満タンで頭打ちの間と、減らないバフ)。
    /// </summary>
    [ObservableProperty]
    private double _barDecaySeconds;

    /// <summary>この行の右クリックのメニューを開いている間。行を強調する。</summary>
    [ObservableProperty]
    private bool _isMenuOpen;

    /// <summary>バフが付いているか。付いていない行(<see cref="CreateInactive"/>)はゲージを出さない。</summary>
    [ObservableProperty]
    private bool _isActive = true;

    /// <summary>デバフの行か。バフ・デバフグループがゲージの色(バフカラー・デバフカラー)を選ぶのに使う。</summary>
    [ObservableProperty]
    private bool _isDebuff;

    /// <summary>残り時間をそのまま持つ。ゲージの長さの設定が変わったら比率を計算し直すのに要る。</summary>
    private double? _remainingSeconds;

    private bool _isRemainingUnknown;

    /// <summary>ゲージの長さが自動のときの長さ(<see cref="PlayerBuffSnapshot.GaugeLengthSeconds"/>)。持続の無いバフは null。</summary>
    private double? _autoGaugeLengthSeconds;

    public void Update(PlayerBuffSnapshot snapshot)
        => Update(snapshot, snapshot.Name);

    /// <param name="name">行に出す名前(<see cref="PlayerBuffEntry(PlayerBuffSnapshot, string)"/>)。</param>
    public void Update(PlayerBuffSnapshot snapshot, string name)
    {
        Key = snapshot.Key;
        CardKey = snapshot.CardKey;
        Uuid = snapshot.Uuid;
        BaseId = snapshot.BaseId;
        Name = name;
        // 1スタックは数字を出さない。重なっているときだけ意味がある。
        LayerText = snapshot.Layer > 1 ? snapshot.Layer.ToString() : string.Empty;
        DurationText = FormatDuration(snapshot.RemainingSeconds, snapshot.IsRemainingUnknown);
        DurationWithUnitText = FormatDurationWithUnit(snapshot.RemainingSeconds, snapshot.IsRemainingUnknown);
        ApplyIcon(snapshot.IconName);
        IsActive = true;

        _remainingSeconds = snapshot.RemainingSeconds;
        _isRemainingUnknown = snapshot.IsRemainingUnknown;
        _autoGaugeLengthSeconds = snapshot.GaugeLengthSeconds;
    }

    /// <summary>付いていない行にする(<see cref="CreateInactive"/>)。番号・鍵はそのまま。</summary>
    public void UpdateInactive(string name, string? iconName, string inactiveText)
    {
        Name = name;
        LayerText = string.Empty;
        DurationText = string.Empty;
        DurationWithUnitText = inactiveText;
        ApplyIcon(iconName);
        IsActive = false;

        _remainingSeconds = null;
        _isRemainingUnknown = false;
        _autoGaugeLengthSeconds = null;
    }

    private void ApplyIcon(string? iconName)
    {
        IconPath = CombatIconResolver.ResolveBuffIcon(iconName);
        IconMask = CombatIconResolver.ResolveBuffIconMask(iconName);
        IsModuleEffectIcon = CombatDataCatalog.IsModEffectIcon(iconName);
        IsFactorIcon = CombatIconResolver.IsFactorIcon(iconName);
        ShowsUnknownIcon = !string.IsNullOrWhiteSpace(iconName) && IconPath is null && IconMask is null;
    }

    /// <summary>
    /// ゲージの長さを計算し直す。
    ///
    /// <para>
    /// <b>残り時間が分からないバフと持続時間の無いバフは、ずっと満タン</b>(ユーザー決定)。
    /// 減っていかないものを短いゲージで出すと、消えかけに見えるため。
    /// </para>
    /// </summary>
    /// <param name="gaugeLengthSeconds">設定の長さ(秒)。自動なら null で、行の長さ(<see cref="_autoGaugeLengthSeconds"/>)を使う。</param>
    public void UpdateBar(int? gaugeLengthSeconds)
    {
        if (!IsActive)
        {
            // 付いていない行にゲージは無い。
            BarRatio = 0d;
            IsBarFull = false;
            BarDecaySeconds = 0d;
            return;
        }

        var lengthSeconds = gaugeLengthSeconds is { } fixedSeconds
            ? fixedSeconds
            : _autoGaugeLengthSeconds ?? 0d;

        var (ratio, decaySeconds) = _isRemainingUnknown || _remainingSeconds is not { } seconds
            ? (1d, 0d)
            : BuffGaugeBrush.GetBarState(seconds, lengthSeconds);

        BarRatio = ratio;
        IsBarFull = ratio >= 1d;
        BarDecaySeconds = decaySeconds;
    }

    /// <summary>
    /// 残り時間の数字だけ。<b>単位を含めない。</b>
    ///
    /// <para>
    /// プレイヤーリストのスキル枠バッジはアイコンに数字を重ねる設計で、単位が入る余地がない。
    /// 単位付きが要る一覧側は <see cref="DurationWithUnitText"/> を見る。
    /// </para>
    /// </summary>
    private static string FormatDuration(double? seconds, bool isUnknown)
    {
        if (isUnknown)
        {
            // アイコンに重ねる枠なので1文字。記号なので4言語とも同じで、リソースは持たない。
            return UnknownDurationBadgeText;
        }

        var roundedSeconds = RoundSeconds(seconds);

        return roundedSeconds is null
            ? string.Empty
            : roundedSeconds.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// 残り時間を<b>単位まで含めた1つの文字列</b>にしたもの。バフ/デバフ一覧の列で使う。
    ///
    /// <para>
    /// 数字と単位を別要素にすると、固定幅の中で右端を揃えるのが2要素の合成になる。
    /// 1つの文字列にしておけば <c>TextAlignment="Right"</c> だけで揃い、
    /// 持続時間が分からないバフ(空文字)では単位も自然に消える。
    /// </para>
    /// </summary>
    private static string FormatDurationWithUnit(double? seconds, bool isUnknown)
    {
        // 数字の位置に「??」を差し込む。単位は既存フォーマットのものが付くので、
        // en は ??s、ja/zh は ??秒、ko は ??초 になる。リソースの追加は要らない。
        object value;
        if (isUnknown)
        {
            value = UnknownDurationListText;
        }
        else
        {
            var roundedSeconds = RoundSeconds(seconds);
            if (roundedSeconds is null)
            {
                return string.Empty;
            }

            value = roundedSeconds.Value;
        }

        return string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            LocalizationManager.Instance.GetString("Widget_BuffDurationFormat"),
            value);
    }

    private static int? RoundSeconds(double? seconds)
    {
        return seconds is null
            ? null
            : Math.Max(0, (int)Math.Ceiling(seconds.Value));
    }


}
