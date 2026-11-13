// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.Core.CombatRuntime.Protocols;
using StarResonanceDps.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Zproto;

namespace StarResonanceDps.Core.CombatRuntime
{
    public static class GrpcTeamManager
    {
        private static readonly PartyStateStore PartyState = PartyStateStore.Instance;
        private static ETeamMemberType? _currentTeamMemberType;

        internal static void ResetMemberState()
        {
            AppState.PartyTeamId = 0;
            _currentTeamMemberType = null;
            PartyState.ResetUnknown();
        }

        internal static void ProcessEntityTeamId(long entityUuid, long teamId)
        {
            var characterId = Utils.UuidToEntityId(entityUuid);
            var isSelf = entityUuid == MessageManager.currentUserUuid
                || entityUuid == AppState.PlayerUUID
                || (AppState.PlayerUID != 0 && characterId == AppState.PlayerUID);
            var party = PartyState.Current;

            if (isSelf)
            {
                var hasKnownPartyMember = party.MemberIds.Any(memberId => memberId != characterId);
                if (teamId == 0
                    && (AppState.PartyTeamId > 0 || party.TeamId > 0 || hasKnownPartyMember))
                {
                    PlayerRosterProjection.RebuildRoster();
                    return;
                }

                if (teamId == 0 || (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId))
                {
                    _currentTeamMemberType = null;
                }

                AppState.PartyTeamId = teamId;
                if (teamId == 0)
                {
                    PartyState.SetNoParty();
                }
                else
                {
                    PartyState.ApplyKnownMembers(teamId, [characterId]);
                }

                PlayerRosterProjection.RebuildRoster();
                return;
            }

            if (!party.HasCompleteMembership)
            {
                if (teamId != 0 && teamId == AppState.PartyTeamId)
                {
                    PartyState.ApplyKnownMembers(teamId, [characterId]);
                }
                else if (AppState.PartyTeamId != 0)
                {
                    PartyState.MarkNonMember(characterId);
                }

                PlayerRosterProjection.RebuildRoster();
                return;
            }

            PlayerRosterProjection.UpsertPlayer(entityUuid);
        }

        internal static void ProcessGetTeamInfo(GetTeamInfoReply reply, ExtraPacketData extraData)
        {
            if (reply.ErrCode != EErrorCode.ErrSuccess)
            {
                return;
            }

            var baseInfo = reply.BaseInfo;
            if (baseInfo == null || baseInfo.TeamId <= 0)
            {
                ResetMemberState();
                PartyState.SetNoParty();
                PlayerRosterProjection.RebuildRoster();
                return;
            }

            var teamId = baseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = baseInfo.TeamMemberType;

            var groups = baseInfo.TeamMemberGroupInfos.Values.ToArray();
            var memberIds = groups
                .SelectMany(group => group.CharIds)
                .Concat(reply.MemberData.Select(member => member.CharId))
                .Concat(reply.MemberFastSyncData.Keys)
                .Where(characterId => characterId > 0)
                .Distinct()
                .ToArray();
            PartyState.ApplyCompleteMembership(
                teamId,
                _currentTeamMemberType == ETeamMemberType.Five,
                memberIds);
            PartyState.ApplyMemberEnterTimes(
                teamId,
                reply.MemberData.Select(member => (member.CharId, member.EnterTime)));

            foreach (var member in reply.MemberData)
            {
                ApplyTeamMemberSocialData(member, "GetTeamInfo");
            }

            foreach (var memberSyncData in reply.MemberFastSyncData)
            {
                ApplyTeamMemberFastSyncData(memberSyncData.Key, memberSyncData.Value);
            }

            if (groups.Length > 0)
            {
                ApplyAuthoritativeMembership(teamId, _currentTeamMemberType, groups);
            }
            PublishCurrentTeam(teamId);
        }

        internal static void ProcessSocialTeamData(SocialData? socialData)
        {
            var teamData = socialData?.TeamData;
            if (socialData == null
                || AppState.PlayerUID == 0
                || socialData.CharId != AppState.PlayerUID
                || teamData == null
                || teamData.TeamId <= 0
                || !teamData.CharIds.Contains(AppState.PlayerUID))
            {
                return;
            }

            var teamId = teamData.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = teamData.TeamMemberType;

            var memberIds = teamData.CharIds
                .Where(characterId => characterId > 0)
                .Distinct()
                .ToArray();
            var memberIdSet = memberIds.ToHashSet();
            var groupAssignments = teamData.TeamMemberData.Values
                .Where(member => memberIdSet.Contains(member.CharId) && member.GroupId > 0)
                .Select(member => (member.CharId, member.GroupId));
            PartyState.ApplyCompleteMembership(
                teamId,
                _currentTeamMemberType == ETeamMemberType.Five,
                memberIds,
                groupAssignments);
            PartyState.ApplyMemberEnterTimes(
                teamId,
                teamData.TeamMemberData.Values
                    .Where(member => memberIdSet.Contains(member.CharId))
                    .Select(member => (member.CharId, member.EnterTime)));
            foreach (var member in teamData.TeamMemberData.Values)
            {
                if (memberIdSet.Contains(member.CharId))
                {
                    ApplyTeamMemberSocialData(member, "NotifySocialData");
                }
            }

            PublishCurrentTeam(teamId);
        }

        public static void ProcessNoticeUpdateTeamInfo(GrpcTeamNtf.Types.NoticeUpdateTeamInfo vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = vData.VRequest.BaseInfo.TeamMemberType;
            ApplyAuthoritativeMembership(
                teamId,
                _currentTeamMemberType,
                vData.VRequest.BaseInfo.TeamMemberGroupInfos.Values);
            PlayerRosterProjection.RebuildRoster();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }
        }

        public static void ProcessNoticeUpdateTeamMemberInfo(GrpcTeamNtf.Types.NoticeUpdateTeamMemberInfo vData, ExtraPacketData extraData)
        {
            PartyState.ApplyKnownMembers(
                AppState.PartyTeamId,
                vData.VRequest.TeamMemberSocialDatas.Select(member => member.CharId)
                    .Concat(vData.VRequest.TeamMemberSyncDatas.Select(member => member.CharId)));
            PartyState.ApplyMemberEnterTimes(
                AppState.PartyTeamId,
                vData.VRequest.TeamMemberSocialDatas.Select(member => (member.CharId, member.EnterTime)));

            foreach (var member in vData.VRequest.TeamMemberSocialDatas)
            {
                ApplyTeamMemberSocialData(member, "NoticeUpdateTeamMemberInfo");
            }

            foreach (var fastSyncData in vData.VRequest.TeamMemberSyncDatas)
            {
                ApplyTeamMemberFastSyncData(
                    fastSyncData.CharId,
                    fastSyncData);
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessNotifyJoinTeam(GrpcTeamNtf.Types.NotifyJoinTeam vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.BaseInfo.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = vData.VRequest.BaseInfo.TeamMemberType;

            var groups = vData.VRequest.BaseInfo.TeamMemberGroupInfos.Values.ToArray();
            PartyState.ApplyCompleteMembership(
                teamId,
                _currentTeamMemberType == ETeamMemberType.Five,
                groups.SelectMany(group => group.CharIds));
            PartyState.ApplyMemberEnterTimes(
                teamId,
                vData.VRequest.MemberData.Select(member => (member.CharId, member.EnterTime)));

            foreach (var member in vData.VRequest.MemberData)
            {
                ApplyTeamMemberSocialData(member, "NotifyJoinTeam");
            }

            foreach (var memberSyncData in vData.VRequest.MemberSyncDatas)
            {
                ApplyTeamMemberFastSyncData(
                    memberSyncData.Key,
                    memberSyncData.Value);
            }

            ApplyAuthoritativeMembership(
                teamId,
                _currentTeamMemberType,
                groups);
            PublishCurrentTeam(teamId);
        }

        public static void ProcessNotifyLeaveTeam(GrpcTeamNtf.Types.NotifyLeaveTeam vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.CharId > 0)
            {
                PartyState.RemoveMember(vData.VRequest.CharId);
            }

            if (vData.VRequest.CharId == AppState.PlayerUID)
            {
                ResetMemberState();
                AppState.PartyTeamId = 0;
                PartyState.SetNoParty();
                if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
                {
                    EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
                }
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessNoticeTeamDissolve(GrpcTeamNtf.Types.NoticeTeamDissolve vData, ExtraPacketData extraData)
        {
            ResetMemberState();
            AppState.PartyTeamId = 0;
            PartyState.SetNoParty();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", 0);
            }

            PlayerRosterProjection.RebuildRoster();
        }

        public static void ProcessNotifyBeTransferLeader(GrpcTeamNtf.Types.NotifyBeTransferLeader vData, ExtraPacketData extraData)
        {
            var teamId = vData.VRequest.LeaderData.TeamData.TeamId;
            if (AppState.PartyTeamId != 0 && AppState.PartyTeamId != teamId)
            {
                ResetMemberState();
            }

            AppState.PartyTeamId = teamId;
            _currentTeamMemberType = vData.VRequest.LeaderData.TeamData.TeamMemberType;
            PartyState.ApplyKnownMembers(teamId, vData.VRequest.LeaderData.TeamData.CharIds);
            PlayerRosterProjection.RebuildRoster();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }
        }

        public static void ProcessNotifyTeamGroupUpdate(GrpcTeamNtf.Types.NotifyTeamGroupUpdate vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.ErrCode != EErrorCode.ErrSuccess)
            {
                return;
            }

            ApplyAuthoritativeMembership(
                AppState.PartyTeamId,
                _currentTeamMemberType,
                vData.VRequest.TeamMemberGroupInfos.Values);
            PlayerRosterProjection.RebuildRoster();
        }

        /// <summary>
        /// パーティでコンテンツに入るときの承認が始まった(パーティの活動の状態が <c>Voting</c>。ゲームが入場の承認画面を開く)。
        /// ゲームが画面を開かない合奏と週替わりの塔の続けての挑戦(自動で承認する)、入場を始めたのが自分のときは上げない。
        /// 値は入る場所の番号と、マスターの難易度(マスターでなければ 0。名前は <see cref="CombatDataCatalog.GetSceneNameWithoutInternalId"/>)。
        /// パケット処理のスレッドで上がる。
        /// </summary>
        public static event Action<int, int>? TeamEntryVoteStarted;

        public static void ProcessNotifyTeamActivityState(GrpcTeamNtf.Types.NotifyTeamActivityState vData, ExtraPacketData extraData)
        {
            var activity = vData.VRequest?.State;
            if (activity is null
                || activity.State != ETeamActivityState.EteamActivityVoting
                || activity.ActivityType == ETeamActivityType.Ensemble)
            {
                return;
            }

            var scene = activity.AssignSceneParams;
            if (scene is null
                || scene.InitParam?.WeeklyTowerInfo?.Continuous == true
                || (AppState.PlayerUID != 0 && scene.CreatorCharId == AppState.PlayerUID))
            {
                return;
            }

            TeamEntryVoteStarted?.Invoke(scene.SceneId, scene.InitParam?.MasterModeDiff ?? 0);
        }

        public static void ProcessTeamActivityResult(GrpcTeamNtf.Types.TeamActivityResult vData, ExtraPacketData extraData)
        {

        }

        public static void ProcessTeamActivityListResult(GrpcTeamNtf.Types.TeamActivityListResult vData, ExtraPacketData extraData)
        {

        }

        public static void ProcessTeamActivityVoteResult(GrpcTeamNtf.Types.TeamActivityVoteResult vData, ExtraPacketData extraData)
        {
            if (vData.VRequest.VCharId == AppState.PlayerUID)
            {
                if (vData.VRequest.Code == ETeamVoteRet.Agree)
                {

                }
                else
                {

                }
            }
        }

        public static void ProcessNotifyCharMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        public static void ProcessNotifyTeamMatchResult(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        public static void ProcessNotifyCharAbortMatch(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        public static void ProcessNotifyTeamEnterErr(ReadOnlySpan<byte> payloadBuffer, ExtraPacketData extraData)
        {

        }

        /// <summary>
        /// NotifySocialData に同梱されるメンバーの social data はサーバ側の非正規化スナップショットで、
        /// <b>メンバーがクラスを変更しても追従しない。</b>AOI や GetTeamInfo が現在値を返していても、
        /// この経路だけ変更前の職業を送り続けて表示を巻き戻す。
        /// より確かなソースから取得済みなら採用せず、まだ何も無いときだけ初期値として使う。
        /// </summary>
        private const string StaleSocialDataSource = "NotifySocialData";

        private static void ApplyTeamMemberSocialData(TeamMemData member, string source)
        {
            if (member.CharId <= 0 || member.SocialData == null)
            {
                return;
            }

            var isTrustedSource = !string.Equals(source, StaleSocialDataSource, StringComparison.Ordinal);
            if (!isTrustedSource
                && PartyState.Current.TryGetSupplement(member.CharId, out var existing)
                && existing.HasTrustedSocialData)
            {
                return;
            }

            var socialData = member.SocialData;
            SocialDataStore.Apply(member.CharId, socialData.AvatarInfo);
            var values = SocialDataValues.Read(socialData);

            // いる場所は NotifySocialData だけ古い値を送り続ける(移動しても変わらない)ので、そこからは入れない。
            var sceneInfo = isTrustedSource ? socialData.UserSceneInfo : null;

            PartyState.UpdateSupplement(
                AppState.PartyTeamId,
                member.CharId,
                current => current with
                {
                    Name = values.Name.Length > 0 ? values.Name : current.Name,
                    Level = values.Level > 0 ? values.Level : current.Level,
                    SeasonLevel = values.SeasonLevel > 0 ? values.SeasonLevel : current.SeasonLevel,
                    CombatPower = values.CombatPower > 0 ? values.CombatPower : current.CombatPower,
                    SeasonStrength = values.SeasonStrength > 0 ? values.SeasonStrength : current.SeasonStrength,
                    ProfessionId = values.ProfessionId > 0 ? values.ProfessionId : current.ProfessionId,
                    EquipmentData = values.EquipmentData ?? current.EquipmentData,
                    IsNpc = values.IsNpc ?? current.IsNpc,
                    HasTrustedSocialData = current.HasTrustedSocialData || isTrustedSource,
                    SceneId = sceneInfo?.SceneId ?? current.SceneId,
                    LineId = sceneInfo?.LineId ?? current.LineId,
                    HasSocialData = true
                });
        }

        private static void ApplyTeamMemberFastSyncData(
            long fallbackCharId,
            TeamMemberFastSyncData fastSyncData)
        {
            var charId = fastSyncData.CharId > 0 ? fastSyncData.CharId : fallbackCharId;
            if (charId <= 0)
            {
                return;
            }

            // 死亡状態の同期は HP を運んでいても 0 として控える。ダメージの無い即死では、
            // サーバは死亡中も死ぬ前の HP を送ってくる。
            var isDead = fastSyncData.State == (int)EActorState.ActorStateDead;

            // HP も最大HPも 0 の同期は位置などだけの同期で、状態も入っていない(0)。戦闘不能の印は HP を運ぶ同期でだけ下ろす。
            var carriesHp = fastSyncData.Hp > 0 || fastSyncData.MaxHp > 0;

            PartyState.UpdateSupplement(
                AppState.PartyTeamId,
                charId,
                current => current with
                {
                    CurrentHp = isDead
                        ? 0
                        : carriesHp
                            ? Math.Max(fastSyncData.Hp, 0)
                            : current.CurrentHp,
                    MaxHp = fastSyncData.MaxHp > 0 ? fastSyncData.MaxHp : current.MaxHp,
                    IsDead = isDead || (!carriesHp && current.IsDead)
                });
        }

        private static void ApplyAuthoritativeMembership(
            long teamId,
            ETeamMemberType? teamMemberType,
            IEnumerable<TeamMemberGroupInfo> groups)
        {
            ApplyAuthoritativeMembership(
                teamId,
                teamMemberType,
                groups.Select(group => (
                    group.GroupId,
                    CharacterIds: (IEnumerable<long>)group.CharIds)));
        }

        private static void ApplyAuthoritativeMembership(
            long teamId,
            ETeamMemberType? teamMemberType,
            IEnumerable<(int GroupId, IEnumerable<long> CharacterIds)> groups)
        {
            var materializedGroups = groups
                .Select(group => (
                    group.GroupId,
                    CharacterIds: group.CharacterIds.ToArray()))
                .ToArray();

            PartyState.ApplyAuthoritativeMembership(
                teamId,
                teamMemberType == ETeamMemberType.Five,
                materializedGroups.Select(group => (
                    group.GroupId,
                    CharacterIds: (IEnumerable<long>)group.CharacterIds)));
        }

        private static void PublishCurrentTeam(long teamId)
        {
            PlayerRosterProjection.RebuildRoster();
            if (AppState.PlayerUUID != 0 && EncounterManager.Current != null)
            {
                EncounterManager.Current.SetAttrKV(AppState.PlayerUUID, "AttrTeamId", teamId);
            }
        }
    }
}
