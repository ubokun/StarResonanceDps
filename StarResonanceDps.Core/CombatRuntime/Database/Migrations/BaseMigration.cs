// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using Microsoft.Data.Sqlite;
using System.Data.Common;

namespace StarResonanceDps.Core.CombatRuntime.Database.Migrations
{
    public class BaseMigration
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public float MinVersion { get; set; }
        public float NewVersion { get; set; }

        public string ErrorMsg = "";
        public float Progress = 0f;

        public virtual bool RunMigration(DbConnection dbConn, SqliteTransaction tx)
        {
            return false;
        }
    }
}
