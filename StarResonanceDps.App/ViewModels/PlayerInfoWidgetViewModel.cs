// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;

namespace StarResonanceDps.App.ViewModels;

public sealed partial class PlayerInfoWidgetViewModel : PlayerWidgetWindowViewModel, IDisposable
{
    /// <summary>名刺の既定の絵。写真を取れたとき以外はこれを出す。</summary>
    private const string DefaultCardResourceKey = "Icon.IdCard.idcard_common_01";

    /// <summary>表示値。相手も覚えている素性も無い間は null(画面ごと隠す)。値で比べ、前回と同じなら差し替えない(画面を作り直さない)。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlayerInfo))]
    private PlayerInfoEntry? _playerInfo;

    /// <summary>顔の絵。写真を取れたら写真、それ以外(照会がまだ届いていない・写真が無い・取得中・取得に失敗)は既定の絵。</summary>
    [ObservableProperty]
    private ImageSource? _avatarImage;

    /// <summary>名刺の半身の絵。出し方は顔と同じ。</summary>
    [ObservableProperty]
    private ImageSource? _cardImage;

    private readonly PlayerPhotoSlot _avatarSlot;
    private readonly PlayerPhotoSlot _cardSlot;
    private PlayerRosterEntry? _player;

    /// <summary>アイコンカラーの塗り。基底のコンストラクタから描き直しが呼ばれることがあるので、既定値で始める。</summary>
    private PlayerInfoIconPalette _iconPalette = new(WidgetConfigDefaults.CreatePlayerInfoSettings());

    private bool _isDisposed;

    public PlayerInfoWidgetViewModel(
        WidgetListItemViewModel infoWidget,
        long? requestedCharacterId,
        PlayerRosterEntry? initialPlayer)
        : base(infoWidget, requestedCharacterId)
    {
        _iconPalette = new PlayerInfoIconPalette(infoWidget.GetPlayerInfoSettingsSnapshot());
        infoWidget.PlayerInfoSettingsChanged += InfoWidget_PlayerInfoSettingsChanged;
        _avatarSlot = new PlayerPhotoSlot(PlayerPhotoLoader.DefaultAvatarResourceKey, image => AvatarImage = image);
        _cardSlot = new PlayerPhotoSlot(DefaultCardResourceKey, image => CardImage = image);
        // 相手が決まる前から既定の絵を用意する(何も分からない間は画面ごと隠れる)。
        RefreshPhotos(force: true);
        InitializePlayer(initialPlayer);
        // 相手がまだ一覧に載っていなくても、覚えている素性があれば見出しと「不明」を出す。
        RenderPlayerInfo();
    }

    public WidgetListItemViewModel InfoWidget => PlayerWidget;

    /// <summary>何か出すものがあるか。無ければ画面ごと隠して、ほかのウィンドウと同じく何も出さない。</summary>
    public bool HasPlayerInfo => PlayerInfo is not null;

    /// <summary>
    /// 自分の顔の写真(URL から取った絵)を出しているときだけ、その写真。右クリックのメニュー(保存・コピー)はこれがあるときだけ出す。
    /// 既定の絵とゲームの用意したアイコンには出さない。
    /// </summary>
    public PlayerPhoto? SavableAvatarPhoto => _player?.IsSelf == true ? _avatarSlot.Photo : null;

    /// <summary>自分の名刺の写真を出しているときだけ、その写真。扱いは顔と同じ。</summary>
    public PlayerPhoto? SavableCardPhoto => _player?.IsSelf == true ? _cardSlot.Photo : null;

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        InfoWidget.PlayerInfoSettingsChanged -= InfoWidget_PlayerInfoSettingsChanged;
    }

    /// <summary>アイコンカラーの設定(保存かプレビュー)が変わった。塗りを作り直す。</summary>
    private void InfoWidget_PlayerInfoSettingsChanged(object? sender, EventArgs e)
    {
        _iconPalette = new PlayerInfoIconPalette(InfoWidget.GetPlayerInfoSettingsSnapshot());
        RenderPlayerInfo();
    }

    /// <summary>相手が一覧に居なくても分かっている値で出す(一覧から外れた人は最後の値で、現在地は「不明」)。顔と名刺は同じ相手なので取り直さない。</summary>
    protected override bool ShowsKnownPlayerOffList => true;

    protected override void OnSelectedPlayerChanged(PlayerRosterEntry? player)
    {
        // 窓を開いたとき(新しい窓は最初の相手が必ず「別の相手」になる)と相手が変わったときは、同じ URL でも取り直す。
        var isOtherPlayer = player?.CharacterId != _player?.CharacterId;
        _player = player;
        RenderPlayerInfo();
        RefreshPhotos(force: isOtherPlayer);
    }

    /// <summary>
    /// 現在地と、ライブの行のシーズン・シーズン階級は一覧の通知の場所とシーズンから決まるので、それが変わったら作り直す
    /// (履歴の回から来た行は、行が持つその回のシーズン)。
    /// </summary>
    protected override void OnRosterContextChanged()
    {
        RenderPlayerInfo();
    }

    /// <summary>写真の URL が変わり得る。写真は URL が変わったときだけ取り直す。</summary>
    protected override void OnSocialDataChanged()
    {
        RefreshPhotos(force: false);
    }

    /// <summary>
    /// ログアウト。出していた行は基底が捨てる(相手が変わったときと同じく既定の絵に戻る)。
    /// 行が無い表示は覚えている素性(捨てた NPC の印と職業を含む)から作るので出し直す。
    /// </summary>
    protected override void OnResetToStartup()
    {
        RenderPlayerInfo();
    }

    protected override WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception, PlayerRosterEntry? player)
    {
        var hasPhoto = player is not null
            && (player.RecordedAvatar ?? SocialDataStore.GetAvatar(player.CharacterId)) is not null;
        return WidgetDataStatusRules.ForPlayerInfo(
            reception,
            EncounterHistoryProvider.HasLoadFailed,
            IsSelfTarget,
            player,
            hasPhoto);
    }

    /// <summary>相手がまだ一覧に載っていない間は、覚えている素性(保存から戻した名前と UID)が入ったら出し直す。</summary>
    protected override void OnLastKnownPlayerChanged()
    {
        if (_player is null)
        {
            RenderPlayerInfo();
        }
    }

    private void RenderPlayerInfo()
    {
        // 相手も覚えている素性も無い(タイトルに名前が出ない)間は何も出さない。
        if (_player is null
            && string.IsNullOrWhiteSpace(LastKnownPlayerName)
            && LastKnownPlayerUid == 0)
        {
            PlayerInfo = null;
            return;
        }

        PlayerInfo = _player is null
            ? PlayerInfoEntry.CreateUnknown(
                LastKnownPlayerName,
                LastKnownPlayerUid,
                IsSelfTarget,
                LastKnownIsNpc,
                LastKnownProfessionId,
                RosterSeasonId,
                _iconPalette)
            : PlayerInfoEntry.Create(
                _player,
                RosterMapName,
                RosterMapChannel,
                _player.SeasonId ?? RosterSeasonId,
                _iconPalette,
                IsProfileUnknown(_player));
    }

    /// <summary>
    /// 冒険者レベル・能力スコアなどがまだ届いていないか。自分は途中起動(全体の値がまだ)、他の人は素性の欠け(周りでの出現も名刺も届いていない)。
    /// </summary>
    private static bool IsProfileUnknown(PlayerRosterEntry player)
    {
        return player.IsSelf
            ? GameReceptionStateStore.Current.IsMidLaunch
            : player.DataGaps.HasFlag(PlayerDataGaps.Identity);
    }

    /// <summary>
    /// 写真のリンクは、履歴の回から来た行ならその回に保存した値、無ければ(ライブの行・保存の時点で届いていなかった人・前の回)名刺の店の今の値。
    /// </summary>
    private void RefreshPhotos(bool force)
    {
        var characterId = _player?.CharacterId ?? 0;
        var avatar = characterId == 0 ? null : _player?.RecordedAvatar ?? SocialDataStore.GetAvatar(characterId);
        _avatarSlot.Show(PlayerPhotoLoader.GetAvatarPhotoUrl(avatar), PlayerPhotoLoader.GetAvatarPresetPath(avatar), characterId, force);
        _cardSlot.Show(PlayerPhotoLoader.GetCardPhotoUrl(avatar), null, characterId, force);
    }
}
