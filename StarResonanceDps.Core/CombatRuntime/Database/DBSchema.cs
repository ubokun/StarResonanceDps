// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Dapper;
using System.Data;

namespace StarResonanceDps.Core.CombatRuntime
{
    public class DBSchema
    {
        public static void CreateTables(IDbConnection conn)
        {
            conn.Execute(Encounter.CreateTable);
            conn.Execute(Entities.CreateTable);
            conn.Execute(Battles.CreateTable);
            conn.Execute(DbData.CreateTable);
        }

        public static class Encounter
        {
            public const string Insert = @"
                INSERT INTO Encounters (
                    BattleId, SceneId, SceneName, SceneSubName, BossUUID, BossAttrId, BossName, BossHpPct, Note, StartTime, EndTime, LastUpdate,
                    TotalDamage, TotalShieldBreak,
                    TotalHealing, TotalOverhealing,
                    TotalDeaths, TotalNpcDeaths, IsWipe, ExDataBlob
                ) VALUES (
                    @BattleId, @SceneId, @SceneName, @SceneSubName, @BossUUID, @BossAttrId, @BossName, @BossHpPct, @Note, @StartTime, @EndTime, @LastUpdate,
                    @TotalDamage, @TotalShieldBreak,
                    @TotalHealing, @TotalOverhealing,
                    @TotalDeaths, @TotalNpcDeaths, @IsWipe, @ExDataBlob
                );
                SELECT last_insert_rowid();";

            public const string UpdateIsWipe = @"UPDATE Encounters SET IsWipe = @IsWipe WHERE EncounterId = @EncounterId";

            public const string SelectAll = @"SELECT * FROM Encounters ORDER BY StartTime DESC";
            public const string SelectById = @"SELECT * FROM Encounters WHERE EncounterId = @EncounterId";
            public const string SelectByBattleId =
                @"SELECT * FROM Encounters WHERE BattleId = @BattleId ORDER BY StartTime";

            public const string SelectOneByBattleId =
                @"SELECT * FROM Encounters WHERE BattleId = @BattleId ORDER BY StartTime LIMIT 1";

            // 残すのは新しい順に @Keep 件。並びは SelectAll(履歴の一覧)と同じ基準。
            public const string RemoveEncountersOverLimit =
                @"DELETE FROM Encounters WHERE EncounterId NOT IN (
                      SELECT EncounterId FROM Encounters ORDER BY StartTime DESC LIMIT @Keep);";

            public const string RemoveEncounter =
                @"DELETE FROM Encounters WHERE EncounterId = @EncounterId;";

            public const string CreateTable = @"
                CREATE TABLE IF NOT EXISTS Encounters (
                    EncounterId INTEGER PRIMARY KEY AUTOINCREMENT,
                    BattleId INTEGER NOT NULL,
                    SceneId INTEGER NOT NULL,
                    SceneName TEXT,
                    SceneSubName TEXT,
                    BossUUID INTEGER DEFAULT 0,
                    BossAttrId INTEGER DEFAULT 0,
                    BossName TEXT,
                    BossHpPct INTEGER DEFAULT 0,
                    Note TEXT,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT,
                    LastUpdate TEXT,
                    TotalDamage INTEGER DEFAULT 0,
                    TotalShieldBreak INTEGER DEFAULT 0,
                    TotalHealing INTEGER DEFAULT 0,
                    TotalOverhealing INTEGER DEFAULT 0,
                    TotalDeaths INTEGER DEFAULT 0,
                    TotalNpcDeaths INTEGER DEFAULT 0,
                    IsWipe INTEGER DEFAULT 0,
                    ExDataBlob BLOB
                )";
        }

        public static class Entities
        {
            public const string Insert = @"
                INSERT INTO Entities (
                    EncounterId, Data
                ) VALUES (
                    @EncounterId, @Data
                );
                SELECT last_insert_rowid();";

            public const string SelectByEncounterId = @"SELECT * FROM Entities WHERE EncounterId = @EncounterId";

            public const string DeleteEntitiesCachesWithNoEncounters =
                @"DELETE FROM Entities WHERE NOT EXISTS (SELECT 1 FROM Encounters WHERE Encounters.EncounterId = Entities.EncounterId);";

            public const string RemoveByEncounterId =
                @"DELETE FROM Entities WHERE EncounterId = @EncounterId;";

            public const string CreateTable = @"
                CREATE TABLE IF NOT EXISTS Entities (
                    EncounterId INTEGER PRIMARY KEY AUTOINCREMENT,
                    Data BLOB NOT NULL
                );";
        }

        public static class Battles
        {
            public const string Insert = @"
                INSERT INTO Battles (
                    SceneId, SceneName, StartTime
                ) VALUES (
                    @SceneId, @SceneName, @StartTime
                );
                SELECT last_insert_rowid();";

            public const string Update = @"UPDATE Battles SET SceneId = @SceneId, SceneName = @SceneName WHERE BattleId = @BattleId";
            public const string UpdateEndTime = @"UPDATE Battles SET EndTime = @EndTime WHERE BattleId = @BattleId";
            public const string SelectByBattleId = @"SELECT * FROM Battles WHERE BattleId = @BattleId";
            public const string SelectAll = @"SELECT * FROM Battles WHERE EndTime NOT NULL";

            public const string DeleteBattlesWithNoEncounters =
                @"DELETE FROM Battles WHERE NOT EXISTS (SELECT 1 FROM Encounters WHERE Encounters.BattleId = Battles.BattleId);";

            public const string CreateTable = @"
                CREATE TABLE IF NOT EXISTS Battles (
                    BattleId INTEGER PRIMARY KEY AUTOINCREMENT,
                    SceneId INT NOT NULL,
                    SceneName TEXT NOT NULL,
                    StartTime TEXT NOT NULL,
                    EndTime TEXT
                );";
        }

        public static class DbData
        {
            public const string Select = @"SELECT * FROM DbData";
            public const string Update = @"UPDATE DbData SET Version = @Version";
            public const string Delete = @"DELETE FROM DbData";

            public const string CreateTable = @"
                CREATE TABLE IF NOT EXISTS DbData (
                    Version REAL
                );

                INSERT INTO DbData (Version) SELECT (1.2) WHERE NOT EXISTS (SELECT 1 FROM DbData)";
        }

    }
}
