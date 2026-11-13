// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.Plugins.ModuleTool.Services;

/// <summary>
/// 最後に受け取った有効な所持モジュールと、受け取った時刻。モジュールツールの窓を開き直しても残す。
///
/// <para>
/// 本体の控えが消えた知らせ(ログアウト・キャプチャの停止)では消さず、次に届くまで持ち続ける。
/// 届いた所持モジュールの中身が持っているものと同じなら(マップ移動のたびに届く)、受け取った時刻だけを更新する。
/// </para>
/// </summary>
internal sealed class ModuleInventoryHolder : IDisposable
{
    private readonly IPluginGameDataService _gameData;

    public ModuleInventoryHolder(IPluginGameDataService gameData)
    {
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        Inventory = _gameData.SelfModules;
        _gameData.SelfModulesChanged += GameData_SelfModulesChanged;
    }

    /// <summary>持っている所持モジュール。一度も受け取っていなければ <c>null</c>。</summary>
    public PluginModuleInventory? Inventory { get; private set; }

    /// <summary>この起動の中で最後に受け取った時刻。プラグインを読み込む前に届いた分は <c>null</c>。</summary>
    public DateTime? ReceivedAt { get; private set; }

    /// <summary>中身の違う所持モジュールを受け取った(画面のスレッド)。</summary>
    public event EventHandler? InventoryChanged;

    /// <summary>中身の同じ所持モジュールを受け取り直した(画面のスレッド)。変わったのは <see cref="ReceivedAt"/> だけ。</summary>
    public event EventHandler? ReceivedAgain;

    public void Dispose()
    {
        _gameData.SelfModulesChanged -= GameData_SelfModulesChanged;
    }

    private void GameData_SelfModulesChanged(object? sender, EventArgs e)
    {
        var received = _gameData.SelfModules;
        if (received is null)
        {
            return;
        }

        ReceivedAt = DateTime.Now;
        if (Inventory is not null && HasSameContent(Inventory, received))
        {
            ReceivedAgain?.Invoke(this, EventArgs.Empty);
            return;
        }

        Inventory = received;
        InventoryChanged?.Invoke(this, EventArgs.Empty);
    }

    // モジュールは本体が uuid の順で渡す。
    private static bool HasSameContent(PluginModuleInventory left, PluginModuleInventory right)
    {
        if (left.Modules.Count != right.Modules.Count || left.EquippedUuidBySlot.Count != right.EquippedUuidBySlot.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Modules.Count; index++)
        {
            var leftModule = left.Modules[index];
            var rightModule = right.Modules[index];
            if (leftModule.Uuid != rightModule.Uuid
                || leftModule.ConfigId != rightModule.ConfigId
                || leftModule.Quality != rightModule.Quality
                || !leftModule.Effects.SequenceEqual(rightModule.Effects))
            {
                return false;
            }
        }

        foreach (var (slot, uuid) in left.EquippedUuidBySlot)
        {
            if (!right.EquippedUuidBySlot.TryGetValue(slot, out var rightUuid) || rightUuid != uuid)
            {
                return false;
            }
        }

        return true;
    }
}
