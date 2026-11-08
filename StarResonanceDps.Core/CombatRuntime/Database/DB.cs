using StarResonanceDps.Core.CombatRuntime.Database;
using StarResonanceDps.Core.CombatRuntime.Database.Migrations;
using StarResonanceDps.Core.CombatRuntime.DataTypes;
using Dapper;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using Serilog;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace StarResonanceDps.Core.CombatRuntime
{
    public class DB
    {
        public static event Action<Encounter>? EncounterSaved;
        public static string DbFilePath => CombatRuntimePaths.DatabasePath;
        public static MigrationStatus MigrationStatus = new MigrationStatus();

        private static SqliteConnection DbConn = null!;
        private static ILogger Log = null!;
        private static ZstdSharp.Compressor Compressor = new ZstdSharp.Compressor();
        private static ZstdSharp.Decompressor Decompressor = new ZstdSharp.Decompressor();

        /// <summary>
        /// 接続(<see cref="DbConn"/>)と共有の圧縮器・展開器を触る処理は全部この中で行う。
        /// 保存はパケットのスレッド、履歴の読み込みは裏のスレッド、一覧は画面のスレッドから来るので、同じ接続を同時に使わせない。
        /// </summary>
        private static object DBLock = new object();
        private static readonly RuntimeSerializationBinder SerializationBinder = new();
        private static readonly EncounterBlobContractResolver BlobContractResolver = new();
        private static readonly List<BaseMigration> Migrations = [];

        public static void Init()
        {
            SQLitePCL.Batteries_V2.Init();
            Log = Log ?? Serilog.Log.Logger.ForContext<DB>();
            if (DbConn != null)
            {
                DbConn.Close();
                DbConn.Dispose();
            }

            DbConn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DbFilePath }.ToString());
            DbConn.Open();

            DBSchema.CreateTables(DbConn);
        }

        public static void CloseAndSave()
        {
            if (DbConn != null)
            {
                lock (DBLock)
                {
                    DbConn.Close();
                }
            }
        }

        public static ulong GetNextEncounterId()
        {
            // AUTOINCREMENT is not reset when the latest rows are deleted.
            const string sql = "SELECT MAX(COALESCE((SELECT MAX(EncounterId) FROM Encounters), 0), COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'Encounters'), 0)) + 1";
            lock (DBLock)
            {
                return DbConn.QuerySingle<ulong>(sql);
            }
        }

        public static ulong GetNumEncounters()
        {
            const string sql = "SELECT COUNT(*) FROM Encounters";
            lock (DBLock)
            {
                return DbConn.QuerySingle<ulong>(sql);
            }
        }

        public static ulong InsertEncounter(Encounter encounter)
        {
            // トランザクションの開始から確定までロックの中。外にあると、開いている間に別のスレッドの読み込みが同じ接続を使う。
            lock (DBLock)
            {
                using var transaction = DbConn.BeginTransaction();
                var result = InsertEncounter(encounter, transaction);
                transaction.Commit();
                EncounterSaved?.Invoke(encounter);

                // 上限を超えたぶんはその場で落とす。終了時だけの掃除だと、稼働中は上限を超えて溜まる。
                if (CombatRuntimeSettings.DatabaseMaxEncounterCount > 0)
                {
                    TrimEncountersToLimit(CombatRuntimeSettings.DatabaseMaxEncounterCount, vacuum: false);
                }

                return result;
            }
        }

        public static ulong InsertEncounter(Encounter encounter, SqliteTransaction tx)
        {
            var sw = Stopwatch.StartNew();
            lock (DBLock)
            {
                // Keep builds inside the same encounter blob, without a separate user library or association.
                foreach (var entity in encounter.Entities.Values.Where(x => x.EntityType == Zproto.EEntityType.EntChar && !x.IsNpc))
                    entity.RecordedBuild ??= RecordedPlayerBuildSnapshot.Capture(entity);
                using var encMs = new MemoryStream();
                encounter.FillSkillActivationsForSave();
                try
                {
                    ProtoBuf.Serializer.Serialize(encMs, encounter.ExData);
                }
                finally
                {
                    encounter.ClearSkillActivationsAfterSave();
                }

                encMs.Flush();
                encounter.ExDataBlob = Compressor.Wrap(encMs.ToArray()).ToArray();

                var encounterId = DbConn.QuerySingle<ulong>(DBSchema.Encounter.Insert, encounter, tx);
                encounter.EncounterId = encounterId;
                var entityBlob = CreateEntityBlobForEncounter(encounter);

                Log.Information($"Enounter's entityBlob.Data.Length = {entityBlob.Data.Length}");
                DbConn.Execute(DBSchema.Entities.Insert, entityBlob, tx);

                GC.Collect(2);

                sw.Stop();
                Log.Information("Saving encounter {encounterId} to DB took: {duration}", encounterId, sw.Elapsed);

                return encounterId;
            }
        }

        public static EntityBlobTable CreateEntityBlobForEncounter(Encounter encounter)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var compStream = new ZstdSharp.CompressionStream(memoryStream))
                {
                    using (var streamWriter = new StreamWriter(compStream, Encoding.UTF8, 1024, true))
                    {
                        using (var writer = new JsonTextWriter(streamWriter))
                        {
                            JsonSerializer serializer = new JsonSerializer()
                            {
                                Formatting = Formatting.None,
                                TypeNameHandling = TypeNameHandling.All,
                                ContractResolver = BlobContractResolver,
                            };
                            lock (encounter.Entities)
                            {
                                encounter.FillTakenSnapshotsForSave();
                                try
                                {
                                    serializer.Serialize(writer, SelectPersistedEntities(encounter));
                                }
                                finally
                                {
                                    encounter.ClearTakenSnapshotsAfterSave();
                                }
                            }
                            writer.Flush();
                        }
                        streamWriter.Flush();
                    }
                    compStream.Flush();
                }

                var entityBlob = new EntityBlobTable();
                entityBlob.EncounterId = encounter.EncounterId;
                entityBlob.Data = memoryStream.ToArray();

                return entityBlob;
            }
        }

        /// <summary>
        /// blob に書くエンティティ。プレイヤー全員と、タイムライン・被ダメログに出てくる加害者・詠唱した敵・敵の回復・ダメージ・バフの行動した側と相手だけ。
        /// それ以外(弾・設置物・タイムライン・被ダメログに出ないモンスターなど)は履歴で読まれない。
        /// 書く項目は <see cref="EncounterBlobContractResolver"/> が決める。
        /// </summary>
        private static ConcurrentDictionary<long, Entity> SelectPersistedEntities(Encounter encounter)
        {
            var persisted = new ConcurrentDictionary<long, Entity>();
            foreach (var uuid in encounter.GetEnemyActionEntityUuids())
            {
                if (encounter.Entities.TryGetValue(uuid, out var healEntity))
                {
                    persisted[uuid] = healEntity;
                }
            }

            foreach (var (uuid, entity) in encounter.Entities)
            {
                if (entity.EntityType != Zproto.EEntityType.EntChar)
                {
                    if (entity.GetSkillCastsCopy().Length > 0)
                    {
                        persisted[uuid] = entity;
                    }

                    continue;
                }

                persisted[uuid] = entity;
                if (entity.TakenStats.SkillSnapshots is not { } snapshots)
                {
                    continue;
                }

                foreach (var snapshot in snapshots)
                {
                    if (encounter.Entities.TryGetValue(snapshot.OtherUUID, out var attacker))
                    {
                        persisted[snapshot.OtherUUID] = attacker;
                    }
                }
            }

            return persisted;
        }

        public static void UpdateEncounterWipeState(ulong encounterId, bool isWipe)
        {
            lock (DBLock)
            {
                DbConn.Execute(DBSchema.Encounter.UpdateIsWipe, new { EncounterId = encounterId, IsWipe = isWipe });
            }

            Log.Information("Updated {encounterId} IsWipe to: {isWipe}", encounterId, isWipe);
        }

        /// <summary>
        /// 保存済みの回を1件読む。履歴の選択では裏のスレッドから呼ぶ(<c>EncounterHistoryProvider.SelectAsync</c>)。
        /// 接続と共有の展開器を触る間(問い合わせ2つと ExData の展開)はロックの中、実体の JSON の復元は呼ぶたびに別のストリームなのでロックの外。
        /// </summary>
        public static Encounter? LoadEncounter(ulong encounterId)
        {
            var sw = Stopwatch.StartNew();
            Encounter? encounter;
            EntityBlobTable? entityBlob;
            lock (DBLock)
            {
                encounter = DbConn.QuerySingleOrDefault<Encounter>(DBSchema.Encounter.SelectById, new { EncounterId = encounterId });
                if (encounter == null)
                {
                    Log.Warning("Encounter {encounterId} not found in database", encounterId);
                    return null;
                }

                var decompressedEncEx = Decompressor.Unwrap(encounter.ExDataBlob);
                ProtoBuf.Serializer.Deserialize<EncounterExData>(decompressedEncEx, encounter.ExData);
                encounter.ExDataBlob = null!;

                entityBlob = DbConn.QuerySingleOrDefault<EntityBlobTable>(DBSchema.Entities.SelectByEncounterId, new { EncounterId = encounterId });
            }

            if (entityBlob?.Data != null)
            {
                using (var memStream = new MemoryStream(entityBlob.Data))
                {
                    using (var decompStream = new ZstdSharp.DecompressionStream(memStream))
                    {
                        using (var streamReader = new StreamReader(decompStream))
                        {
                            using (JsonTextReader reader = new JsonTextReader(streamReader))
                            {
                                JsonSerializer serializer = new JsonSerializer()
                                {
                                    ContractResolver = new PrivateResolver(),
                                    ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                                    TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
                                    SerializationBinder = SerializationBinder
                                };
                                serializer.Converters.Add(new DictionaryObjectConverter());

                                encounter.Entities = serializer.Deserialize<ConcurrentDictionary<long, Entity>>(reader)!;
                            }
                        }
                    }
                }

                // GC.Collect(2) はここに置かない。**世代2のフル回収は全部のスレッドを止める。**
                // 履歴の選択は裏のスレッドから走るが、回収の間は画面のスレッドも止まる。
                // blob が小さくても1秒以上持っていかれる。一時バッファの回収はランタイムに任せる。
            }
            else
            {
                Log.Warning("Entities data not found for encounter {encounterId}", encounterId);
                encounter.Entities = new ConcurrentDictionary<long, Entity>();
            }

            sw.Stop();
            Log.Information("Loading encounter {encounterId} from DB took: {duration}", encounterId, sw.Elapsed);

            return encounter;
        }

        public static List<Encounter> LoadEncounterSummaries()
        {
            lock (DBLock)
            {
                var encounters = DbConn.Query<Encounter>(DBSchema.Encounter.SelectAll).ToList();
                foreach (var encounter in encounters)
                {
                    var decompressedEncEx = Decompressor.Unwrap(encounter.ExDataBlob);
                    ProtoBuf.Serializer.Deserialize<EncounterExData>(decompressedEncEx, encounter.ExData);
                    encounter.ExDataBlob = null!;
                }

                return encounters;
            }
        }

        public static EncounterExData? GetEncounterExDataForBattle(int battleId)
        {
            try
            {
                lock (DBLock)
                {
                    var encounter = DB.DbConn.QueryFirst<Encounter>(DBSchema.Encounter.SelectOneByBattleId, new { BattleId = battleId });
                    var decompressedEncEx = Decompressor.Unwrap(encounter.ExDataBlob);
                    ProtoBuf.Serializer.Deserialize<EncounterExData>(decompressedEncEx, encounter.ExData);
                    encounter.ExDataBlob = null!;

                    return encounter.ExData;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting GetEncounterExDataForBattle by {battleId}", battleId);
            }

            return null;
        }

        /// <param name="maxCount">残す最大の件数。<b>0 は無限</b>で、呼び出し側が呼ばない。</param>
        /// <param name="vacuum">DB を詰め直すか。保存のたびに回すには重いので、終了時だけ真にする。</param>
        public static DBCleanUpResults TrimEncountersToLimit(int maxCount, bool vacuum)
        {
            lock (DBLock)
            {
                var results = new DBCleanUpResults();
                results.EncountersDeleted = DbConn.Execute(DBSchema.Encounter.RemoveEncountersOverLimit, new { Keep = maxCount });
                results.EntitiesCachesDeleted = DbConn.Execute(DBSchema.Entities.DeleteEntitiesCachesWithNoEncounters);
                results.BattlesDeleted = DbConn.Execute(DBSchema.Battles.DeleteBattlesWithNoEncounters);

                if (vacuum)
                {
                    DbConn.Execute("VACUUM");
                }

                Log.Information("Cleaned up {EncountersDeleted} encounters and {BattlesDeleted} battles, with {EntitesCachesDeleted} cachedEntities",
                        results.EncountersDeleted, results.BattlesDeleted, results.EntitiesCachesDeleted);

                return results;
            }
        }

        public static void DeleteEncounter(ulong encounterId)
        {
            lock (DBLock)
            {
                var tx = DbConn.BeginTransaction();
                DbConn.Execute(DBSchema.Encounter.RemoveEncounter, new { EncounterId = encounterId }, tx);
                DbConn.Execute(DBSchema.Entities.RemoveByEncounterId, new { EncounterId = encounterId }, tx);
                tx.Commit();

                Log.Information("Deleted Encounter: {encounterId}", encounterId);
            }
        }

        public static int GetNextBattleId()
        {
            const string sql = "SELECT COALESCE(MAX(BattleId), 0) + 1 FROM Battles";
            lock (DBLock)
            {
                return DbConn.QuerySingle<int>(sql);
            }
        }

        public static int StartBattle(uint sceneId, string sceneName)
        {
            var battle = new Battle()
            {
                SceneId = sceneId,
                SceneName = sceneName ?? "",
                StartTime = DateTime.Now
            };

            lock (DBLock)
            {
                var battleId = DbConn.QuerySingle<int>(DBSchema.Battles.Insert, battle);

                return battleId;
            }
        }

        public static void UpdateBattleInfo(int battleId, uint sceneId, string sceneName)
        {
            var battle = new Battle()
            {
                BattleId = battleId,
                SceneId = sceneId,
                SceneName = sceneName ?? ""
            };

            lock (DBLock)
            {
                DbConn.Execute(DBSchema.Battles.Update, battle);
            }
        }

        public static void UpdateBattleEnd(int battleId)
        {
            var battle = new Battle()
            {
                BattleId = battleId,
                EndTime = DateTime.Now
            };

            lock (DBLock)
            {
                DbConn.Execute(DBSchema.Battles.UpdateEndTime, battle);
            }
        }

        public static List<Battle> LoadBattles()
        {
            lock (DBLock)
            {
                var battles = DbConn.Query<Battle>(DBSchema.Battles.SelectAll).ToList();
                return battles;
            }
        }

        public static List<Encounter> LoadEncountersForBattleId(int battleId)
        {
            List<Encounter> encountersSum;
            lock (DBLock)
            {
                encountersSum = DbConn.Query<Encounter>(DBSchema.Encounter.SelectByBattleId, new { BattleId = battleId }).ToList();
            }

            var encounters = new List<Encounter>(encountersSum.Count);
            foreach (var encounter in encountersSum)
            {
                var encounterFull = LoadEncounter(encounter.EncounterId);
                encounters.Add(encounterFull!);
            }

            return encounters;
        }






        public static bool CheckIfMigrationsNeeded()
        {
            lock (DBLock)
            {
                var dbData = DbConn.Query<DbData>(DBSchema.DbData.Select).First();
                var migrationsToRun = Migrations.Where(x => x.MinVersion >= dbData.Version).ToList();

                return migrationsToRun.Count > 0;
            }
        }

        public static void CheckAndRunMigrations()
        {
            // 移行は中で読み込み・保存を呼ぶ(同じスレッドなのでロックは入れ子で取れる)。
            lock (DBLock)
            {
                RunMigrations();
            }
        }

        private static void RunMigrations()
        {
            var dbData = DbConn.Query<DbData>(DBSchema.DbData.Select).First();
            var migrationsToRun = Migrations.Where(x => x.MinVersion >= dbData.Version).ToList();

            if (migrationsToRun.Count() > 0)
            {
                Log.Information("{NumMigrations} Migrations to run", migrationsToRun.Count());
                MigrationStatus.State = MigrationStatusState.Running;
                MigrationStatus.TotalMigrationsNeeded = migrationsToRun.Count();
                MigrationStatus.CurrentMigrationNum = 0;

                foreach (var migration in migrationsToRun)
                {
                    MigrationStatus.CurrentMigrationNum++;
                    MigrationStatus.CurrentMigration = migration;
                    Log.Information("Starting migration {Num}, {Name}, {Description}", MigrationStatus.CurrentMigrationNum, migration.Name, migration.Description);
                    var tx = DbConn.BeginTransaction();
                    var result = migration.RunMigration(DbConn, tx);
                    if (!result)
                    {
                        Log.Error("Error running migration: {Name}", migration.Name);
                        MigrationStatus.State = MigrationStatusState.Error;
                        MigrationStatus.ErrorMsg = migration.ErrorMsg;
                        tx.Rollback();
                    }
                    else
                    {
                        DbConn.Execute(DBSchema.DbData.Update, new { Version = migration.NewVersion }, tx);
                        tx.Commit();
                        Log.Information("Migration {Num} {Name} Ran, Updated to version {Version}", MigrationStatus.CurrentMigrationNum, migration.Name, migration.NewVersion);
                    }
                }

                Log.Information("Migrations ran, cleaning up.");
                MigrationStatus.State = MigrationStatusState.CleanUp;
                DbConn.Execute("VACUUM");
            }

            MigrationStatus.State = MigrationStatusState.Done;
        }
    }

    public class DBCleanUpResults
    {
        public int EncountersDeleted { get; set; } = 0;
        public int BattlesDeleted { get; set; } = 0;
        public int EntitiesCachesDeleted { get; set; } = 0;
    }
}
