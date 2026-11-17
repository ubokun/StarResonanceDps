// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// エンティティ(エンティティリストの実体)用ウィンドウが追う対象。
///
/// <para>
/// 通常は<b>実体1つ</b>に固定される。エンティティリストから選んで開いた窓は、
/// その個体を追い続ける。
/// </para>
///
/// <para>
/// 例外は<b>設定からの復元直後</b>。実体ID(<c>EntityUuid</c>)は再起動で消えるため、
/// 保存できるのは種類と種別ID(<see cref="EntityType"/>・<see cref="EntityId"/>)だけ。復元した窓は未捕獲の状態で開き、
/// 種類と種別が一致する個体が現れた時点で1体だけ捕まえる。捕まえた後は通常と同じで、その個体に固定される。
/// </para>
/// </summary>
public sealed class EntityWindowTarget
{
    private readonly string _savedName;
    private EntityTargetInfo? _entity;

    /// <summary>実体が分かっている状態で作る。エンティティリストからの経路。</summary>
    public EntityWindowTarget(EntityTargetInfo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        EntityId = entity.EntityId;
        EntityType = entity.EntityType;
        _savedName = entity.Name;
        _entity = entity;
    }

    /// <summary>
    /// 種別だけ分かっている状態で作る。設定からの復元経路。
    /// <paramref name="entityType"/> が <c>null</c>(種類を持たない保存)なら、どの個体も捕まえない。
    /// </summary>
    public EntityWindowTarget(long entityId, EEntityType? entityType, string? savedName)
    {
        EntityId = entityId;
        EntityType = entityType;
        _savedName = savedName ?? string.Empty;
    }

    /// <summary>捕まえた個体やその値(名前・レベル)が変わったとき、個体を放したときに上がる。</summary>
    public event EventHandler? Changed;

    public long EntityId { get; }

    /// <summary>実体の種類。<see cref="EntityId"/> がどの表の番号かを決める。</summary>
    public EEntityType? EntityType { get; }

    /// <summary>捕まえている実体のID。<b>0 は未捕獲</b>。</summary>
    public long EntityUuid => _entity?.EntityUuid ?? 0;

    public bool IsAcquired => _entity is not null;

    /// <summary>捕まえている実体の値。未捕獲なら <c>null</c>。</summary>
    public EntityTargetInfo? Entity => _entity;

    /// <summary>ヘッダーに出す名前(書式を通していない素の名前)。未捕獲なら保存しておいた名前。</summary>
    public string Name => _entity?.Name ?? _savedName;

    public int Level => _entity?.Level ?? 0;

    public bool Represents(long entityUuid)
    {
        return IsAcquired && EntityUuid == entityUuid;
    }

    /// <summary>この対象を出す窓(バフ一覧・バフ・デバフカード)の状態の印。条件は <see cref="WidgetDataStatusRules.ForEntityBuffs"/>。</summary>
    public WidgetDataStatus CreateDataStatus(GameReceptionSnapshot reception)
    {
        var isAcquired = IsAcquired;
        return WidgetDataStatusRules.ForEntityBuffs(
            reception,
            isAcquired,
            isAcquired && MeterSnapshotProvider.HasReceivedEntityAppearBuffList(EntityUuid),
            isAcquired && NearbyEntityStore.Instance.Contains(EntityUuid),
            _entity?.IsTypeUnknown == true);
    }

    /// <summary>未捕獲のときに捕まえてよい個体か。種類と種別IDの両方が一致するもの。</summary>
    public bool Matches(EntityTargetInfo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return entity.EntityId == EntityId && entity.EntityType == EntityType;
    }

    /// <summary>
    /// まだ捕まえておらず、その個体を捕まえられるか(<see cref="Matches"/>)。
    /// ほかの窓がその個体を映しているかは、窓の一覧を持つ窓の管理が見る。
    /// </summary>
    public bool CanCapture(EntityTargetInfo entity)
    {
        return _entity is null && Matches(entity);
    }

    /// <summary>
    /// 与えられた個体の値を採用する。未捕獲なら種類と種別が一致するものを捕まえ、
    /// 捕獲済みなら同じ実体の新しい値にだけ差し替える。
    /// </summary>
    /// <returns>採用したら <c>true</c>。</returns>
    public bool TryApply(EntityTargetInfo entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (_entity is null)
        {
            if (!Matches(entity))
            {
                return false;
            }
        }
        else if (entity.EntityUuid != EntityUuid || entity == _entity)
        {
            return false;
        }

        _entity = entity;
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// 捕まえている個体を放し、設定からの復元直後と同じ未捕獲に戻す(ログアウトで起動時の状態へ戻したとき)。
    /// 以後は種類と種別IDが一致する個体を捕まえ直す。
    /// </summary>
    public void Release()
    {
        if (_entity is null)
        {
            return;
        }

        _entity = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
