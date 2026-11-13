// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.ComponentModel;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// エンティティ(モンスター)に紐づくウィジェットウィンドウのViewModel。
///
/// <para>
/// バフ/デバフ一覧とバフ・デバフカードとエンティティ情報の3種類があり、
/// <see cref="Services.WidgetWindowManager"/> は同じ一覧で扱う。
/// </para>
/// </summary>
public interface IEntityWidgetWindowViewModel : INotifyPropertyChanged, IDisposable, IWidgetDataStatusSource
{
    /// <summary>種別ID。復元時に対象を捕まえ直すのに使う。</summary>
    long EntityId { get; }

    /// <summary>実体の種類。<see cref="EntityId"/> がどの表の番号かを決めるので、捕まえ直すときは組で比べる。</summary>
    Zproto.EEntityType? EntityType { get; }

    /// <summary>捕まえている実体のID。<b>0 は未捕獲</b>(復元直後)。</summary>
    long EntityUuid { get; }

    bool IsEntityAcquired { get; }

    /// <summary>敵を待つ窓がまだ個体を捕まえていない(<see cref="Models.Widgets.EntityWindowTarget.CreateWaiting"/>)。</summary>
    bool IsWaitingForEntity { get; }

    /// <summary>捕まえている実体の値。未捕獲なら <c>null</c>。</summary>
    EntityTargetInfo? Entity { get; }

    /// <summary>保存に使う名前。未捕獲なら復元時に読み込んだ名前。</summary>
    string TargetName { get; }

    string HeaderText { get; }

    bool RepresentsEntity(long entityUuid);

    /// <summary>個体の値を採用する。未捕獲なら種別一致で捕まえる。</summary>
    bool TryApplyEntity(EntityTargetInfo entity);

    /// <summary>捕まえている個体を放し、未捕獲に戻す(ログアウトで起動時の状態へ戻したとき)。</summary>
    void ReleaseEntity();

    void RefreshPresentation();
}
