// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.Database.Migrations
{
    public class MigrationStatus
    {
        public MigrationStatusState State { get; set; } = MigrationStatusState.NotStarted;
        public BaseMigration? CurrentMigration { get; set; }
        public int TotalMigrationsNeeded { get; set; } = 0;
        public int CurrentMigrationNum { get; set; } = 0;
        public string ErrorMsg = "";
    }

    public enum MigrationStatusState
    {
        NotStarted,
        Running,
        CleanUp,
        Done,
        Error,
    }
}
