// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグイン1つに渡す、本体が通信から控えた値の口。値は全部のプラグインで共有(<see cref="PluginGameDataSource"/>)し、
/// 知らせはプラグインごとに受け止め(<see cref="PluginCallGuard"/>)の中で出す。
/// </summary>
internal sealed class PluginGameDataService : IPluginGameDataService
{
    private readonly PluginCallGuard _guard;
    private readonly PluginGameDataSource _source;
    private bool _isDetached;

    public PluginGameDataService(PluginCallGuard guard, PluginGameDataSource source)
    {
        _guard = guard ?? throw new ArgumentNullException(nameof(guard));
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _source.SelfModulesChanged += Source_SelfModulesChanged;
        _source.SessionChanged += Source_SessionChanged;
        _source.PlayersChanged += Source_PlayersChanged;
        _source.EntitiesChanged += Source_EntitiesChanged;
        _source.EncounterStarted += Source_EncounterStarted;
        _source.EncounterClosed += Source_EncounterClosed;
        _source.MapEntered += Source_MapEntered;
        _source.LoggedOut += Source_LoggedOut;
    }

    public PluginGameServer CurrentServer => _source.CurrentServer;

    public PluginModuleInventory? SelfModules => _source.SelfModules;

    public PluginSessionState Session => _source.Session;

    public IReadOnlyList<PluginPlayer> Players => _source.Players;

    public IReadOnlyList<PluginEntity> Entities => _source.Entities;

    public event EventHandler? SelfModulesChanged;

    public event EventHandler? SessionChanged;

    public event EventHandler? PlayersChanged;

    public event EventHandler? EntitiesChanged;

    public event EventHandler? EncounterStarted;

    public event EventHandler<PluginEncounterResult>? EncounterClosed;

    public event EventHandler? MapEntered;

    public event EventHandler? LoggedOut;

    public IReadOnlyList<PluginBuff> GetPlayerBuffs(long characterId) => _source.GetPlayerBuffs(characterId);

    public IReadOnlyList<PluginBuff> GetEntityBuffs(long entityUuid) => _source.GetEntityBuffs(entityUuid);

    public PluginEncounterSnapshot GetEncounter() => _source.GetEncounter();

    public IReadOnlyList<PluginSkillTotals> GetSkillTotals(long characterId, PluginCombatKind kind) => _source.GetSkillTotals(characterId, kind);

    /// <summary>プラグインを止めた・終えたときに、本体の知らせから外す。</summary>
    public void Detach()
    {
        if (_isDetached)
        {
            return;
        }

        _isDetached = true;
        _source.SelfModulesChanged -= Source_SelfModulesChanged;
        _source.SessionChanged -= Source_SessionChanged;
        _source.PlayersChanged -= Source_PlayersChanged;
        _source.EntitiesChanged -= Source_EntitiesChanged;
        _source.EncounterStarted -= Source_EncounterStarted;
        _source.EncounterClosed -= Source_EncounterClosed;
        _source.MapEntered -= Source_MapEntered;
        _source.LoggedOut -= Source_LoggedOut;
    }

    private void Source_SelfModulesChanged(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Self modules notification", () => SelfModulesChanged?.Invoke(this, EventArgs.Empty));
    }

    private void Source_SessionChanged(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Session notification", () => SessionChanged?.Invoke(this, EventArgs.Empty));
    }

    private void Source_PlayersChanged(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Players notification", () => PlayersChanged?.Invoke(this, EventArgs.Empty));
    }

    private void Source_EntitiesChanged(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Entities notification", () => EntitiesChanged?.Invoke(this, EventArgs.Empty));
    }

    private void Source_EncounterStarted(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Encounter start notification", () => EncounterStarted?.Invoke(this, EventArgs.Empty));
    }

    private void Source_EncounterClosed(object? sender, PluginEncounterResult result)
    {
        _guard.TryInvoke("Encounter close notification", () => EncounterClosed?.Invoke(this, result));
    }

    private void Source_MapEntered(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Map enter notification", () => MapEntered?.Invoke(this, EventArgs.Empty));
    }

    private void Source_LoggedOut(object? sender, EventArgs e)
    {
        _guard.TryInvoke("Logout notification", () => LoggedOut?.Invoke(this, EventArgs.Empty));
    }
}
