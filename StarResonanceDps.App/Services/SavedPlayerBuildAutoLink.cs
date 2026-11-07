using Serilog;
using StarResonanceDps.Core.CombatRuntime;

namespace StarResonanceDps.App.Services;

public static class SavedPlayerBuildAutoLink
{
    private static bool _initialized;
    public static string? LastIssue { get; private set; }
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        DB.EncounterSaved += OnEncounterClosing;
    }

    private static void OnEncounterClosing(Encounter encounter)
    {
        if (!CombatExtensionSettings.Current.AutoLinkSavedPlayers || encounter.ExData.PartyCharacterIds.Count == 0) return;
        try
        {
            var store = new SavedPlayerBuildStore();
            var known = store.Load(out _).Where(x => x.Server == encounter.ExData.GameServer.ToString())
                .Select(x => x.CharacterId).ToHashSet();
            foreach (var id in encounter.ExData.PartyCharacterIds.Where(known.Contains))
            {
                try { store.Save(PlayerBuildCapture.Capture(id, encounter)); }
                catch (InvalidOperationException) { /* No combat record for this roster member. */ }
            }
        }
        catch (Exception ex)
        {
            LastIssue = "ビルドの自動紐づけに失敗しました：" + ex.Message;
            Log.Warning(ex, "Saved player build auto-link failed for encounter {EncounterId}", encounter.EncounterId);
        }
    }
}
