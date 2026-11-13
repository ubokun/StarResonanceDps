// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// エンティティリストの HP バーの右に並ぶヘイトの枠の1つ。中身は「今狙っている相手」かヘイトの順位の1人
/// (どの枠に何を入れるかは <c>EntityListEntry</c> が決める)。
/// 中身はクラスアイコン・プレイヤー名・バッジ(狙っている相手の印か、ヘイトのゲージ)。TIPS は枠全体。
/// 枠は行ごとに作ったまま使い回し、値だけ入れ替える。出すものが無い枠は隠し、中身の部品も作らない。
/// </summary>
public sealed partial class EntityHateSlot : ObservableObject
{
    /// <summary>「今狙っている相手」を入れているか。バッジは狙っている相手の印、それ以外はヘイトのゲージ。</summary>
    [ObservableProperty]
    private bool _isTarget;

    [ObservableProperty]
    private bool _isShown;

    /// <summary>クラスアイコンの下に出すプレイヤー名(ほかの画面と同じプレイヤー名の決まり、名簿に居なければ「不明」)。</summary>
    [ObservableProperty]
    private string _playerName = string.Empty;

    [ObservableProperty]
    private Brush? _classIconMask;

    [ObservableProperty]
    private Brush? _classBrush;

    /// <summary>
    /// 枠の TIPS。特化名の後ろに、括弧で
    /// 「狙われている」(今狙っている相手)か「ヘイト値: 12345」(ヘイトの順位の1人)。
    /// </summary>
    [ObservableProperty]
    private string _toolTipText = string.Empty;

    /// <summary>ヘイトのゲージの割合(0〜1、<see cref="HateBadgeImages.GetGaugeRatio"/>)。塗る高さは画面がゲージの実際の大きさから決める。</summary>
    [ObservableProperty]
    private double _gaugeRatio;

    [ObservableProperty] private double _relativeHateRatio;
    [ObservableProperty] private string _relativeHateText = string.Empty;
    [ObservableProperty] private string _rankLabel = string.Empty;

    public static double GetRelativeHateRatio(long value, double total)
        => total > 0 ? Math.Clamp(value / total, 0d, 1d) : 0d;

    internal void ShowTarget(EntityHatePlayerLook player)
    {
        IsTarget = true;
        RankLabel = "◎"; RelativeHateRatio = 0; RelativeHateText = "";
        ApplyPlayer(player, LocalizationManager.Instance.GetString("EntityList_Targeted"));
        IsShown = true;
    }

    internal void ShowRank(EntityHatePlayerLook player, long hateValue, int rank, double totalHate)
    {
        IsTarget = false;
        RankLabel = rank.ToString();
        RelativeHateRatio = GetRelativeHateRatio(hateValue, totalHate);
        RelativeHateText = (Math.Floor(RelativeHateRatio * 1000) / 10).ToString("0.#") + "%";
        ApplyPlayer(player, LocalizationManager.Instance.Format("EntityList_HateValue", hateValue));
        GaugeRatio = HateBadgeImages.GetGaugeRatio(hateValue);
        IsShown = true;
    }

    internal void Hide()
    {
        IsShown = false;
    }

    /// <param name="detail">TIPS の特化名の後ろの括弧の中。括弧は4言語とも半角で、前に空白を入れない(バフ名の既定の書式と同じ形)。</param>
    private void ApplyPlayer(EntityHatePlayerLook player, string detail)
    {
        ClassIconMask = player.ClassIconMask;
        ClassBrush = player.ClassBrush;
        PlayerName = player.DisplayName;
        ToolTipText = $"{player.ClassSpecName}({detail})";
    }
}

/// <summary>ヘイトの枠に出すプレイヤーの見た目。</summary>
internal readonly record struct EntityHatePlayerLook(
    Brush ClassIconMask,
    Brush ClassBrush,
    string ClassSpecName,
    string DisplayName);

/// <summary>
/// ヘイトの枠に出すプレイヤーの見た目を名簿から引く。エンティティリストの項目(<c>WidgetListItemViewModel</c>)とエンティティ情報の窓が1つずつ持つ。
///
/// <para>
/// 名前・クラス・特化は名簿の行から。名前はほかの画面と同じプレイヤー名の決まり(<see cref="PlayerInfoFormatFormatter.GetDisplayName(string?, long, bool, bool, int, PlayerNameDisplayMode)"/>、
/// 全体設定の表示設定「プレイヤー名」に従う)。特化名はプレイヤーリストと同じ <see cref="PlayerInfoFormatFormatter.GetClassSpecText"/>。
/// 色は持ち主のウィジェットのアイコンカラーのクラスの行。名簿に居ない相手は、はてなのアイコン・名前も特化名も「不明」。
/// </para>
///
/// <para>
/// クラスアイコンの形は職業ごとに1つなので使い回し、色は設定が替わったら作り直す。
/// </para>
/// </summary>
public sealed class EntityHatePlayerResolver
{
    private const string UnknownProfessionKey = "Unknown";

    private readonly Dictionary<string, ImageBrush> _professionIconMasks = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SolidColorBrush> _classBrushes = new(StringComparer.Ordinal);
    private Dictionary<long, PlayerRosterEntry> _rosterByCharacterId = [];
    private EntityRowSettings? _settings;
    private PlayerNameDisplayMode _nameDisplayMode;

    /// <summary>名簿が変わった。同じキャラ番号の行が2つあれば後ろの行(プレイヤーリストと同じ)。</summary>
    public void SetRoster(IReadOnlyList<PlayerRosterEntry> roster)
    {
        ArgumentNullException.ThrowIfNull(roster);

        var rosterByCharacterId = new Dictionary<long, PlayerRosterEntry>(roster.Count);
        foreach (var row in roster)
        {
            rosterByCharacterId[row.CharacterId] = row;
        }

        _rosterByCharacterId = rosterByCharacterId;
    }

    /// <summary>行を当て直す前に、今の設定と名前の見せ方を渡す。設定が替わっていれば色を作り直す。</summary>
    public void Prepare(EntityRowSettings settings, PlayerNameDisplayMode nameDisplayMode)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!ReferenceEquals(settings, _settings))
        {
            _settings = settings;
            _classBrushes.Clear();
        }

        _nameDisplayMode = nameDisplayMode;
    }

    internal EntityHatePlayerLook Resolve(long characterId)
    {
        var settings = _settings ?? throw new InvalidOperationException("Prepare must be called before resolving hate players.");

        if (!_rosterByCharacterId.TryGetValue(characterId, out var row))
        {
            return new EntityHatePlayerLook(
                GetProfessionIconMask(UnknownProfessionKey),
                GetClassBrush(settings, UnknownProfessionKey),
                PlayerInfoFormatFormatter.GetClassSpecText(PlayerClassSpec.Unknown),
                LocalizationManager.Instance.GetString("PlayerInfo_Unknown"));
        }

        var professionKey = PlayerProfession.GetKey(row.ProfessionId, row.ClassSpec);
        return new EntityHatePlayerLook(
            GetProfessionIconMask(professionKey),
            GetClassBrush(settings, professionKey),
            PlayerInfoFormatFormatter.GetClassSpecText(row.ClassSpec),
            PlayerInfoFormatFormatter.GetDisplayName(
                row.Name,
                row.CharacterId,
                row.IsSelf,
                row.IsNpc,
                row.ProfessionId,
                _nameDisplayMode));
    }

    private ImageBrush GetProfessionIconMask(string professionKey)
    {
        if (!_professionIconMasks.TryGetValue(professionKey, out var mask))
        {
            mask = new ImageBrush((ImageSource)Application.Current.FindResource($"Icon.Profession.{professionKey}"))
            {
                Stretch = Stretch.Uniform
            };
            mask.Freeze();
            _professionIconMasks[professionKey] = mask;
        }

        return mask;
    }

    private SolidColorBrush GetClassBrush(EntityRowSettings settings, string professionKey)
    {
        if (!_classBrushes.TryGetValue(professionKey, out var brush))
        {
            brush = new SolidColorBrush(EntityListEntry.GetClassColor(settings, professionKey));
            brush.Freeze();
            _classBrushes[professionKey] = brush;
        }

        return brush;
    }
}
