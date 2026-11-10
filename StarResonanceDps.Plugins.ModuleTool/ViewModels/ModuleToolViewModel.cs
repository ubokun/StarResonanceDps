using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Threading;
using StarResonanceDps.PluginSdk;
using StarResonanceDps.Plugins.ModuleTool.Models;
using StarResonanceDps.Plugins.ModuleTool.Services;

namespace StarResonanceDps.Plugins.ModuleTool.ViewModels;

internal sealed class ModuleToolViewModel : ObservableObject, IDisposable
{
    private const string ConfigFileName = "module_config.json";
    private const string ResourceBaseName = "StarResonanceDps.Plugins.ModuleTool.Properties.Resources";
    private const int ResultCount = 10;
    private const string ConditionSeparator = ", ";
    private static readonly TimeSpan ElapsedRefreshInterval = TimeSpan.FromMilliseconds(100);

    private readonly IPluginContext _context;
    private readonly ModuleCatalog _catalog;
    private readonly ModuleInventoryHolder _inventoryHolder;
    private readonly PluginLocalizer _texts;
    private readonly ModuleToolConfig _config;
    private readonly Dictionary<int, int> _effectIndexById;
    private readonly DispatcherTimer _elapsedTimer;
    private readonly Stopwatch _stopwatch = new();
    private readonly HashSet<long> _reportedUnknownModules = [];

    private PluginModuleInventory? _inventory;
    private IReadOnlyList<ModuleCandidate> _usableModules = [];
    private int _missingLinkModuleCount;
    private int _unknownModuleCount;
    private DateTime? _receivedAt;
    private ModuleCombination? _equipped;
    private IReadOnlyList<ModuleCombination>? _results;
    private long _resultMatchingCount;
    private double _resultElapsedSeconds;

    // カードの効果の並びと結果の上の行に使う、最後に検索したときの条件(検索する前は開いたときの条件)。
    private IReadOnlyList<ModulePriorityEffect> _shownConditions = [];
    private bool _resultsInventoryChanged;

    private CancellationTokenSource? _search;
    private IReadOnlyList<ModulePriorityEffect> _runningConditions = [];
    private bool _runningInventoryChanged;
    private double _progress;
    private bool _hasReportedSaveFailure;
    private bool _pendingSettingsLoadFailedMessage;

    private IReadOnlyList<ModuleEffectOption> _addEffectOptions = [];

    public ModuleToolViewModel(IPluginContext context, ModuleCatalog catalog, ModuleInventoryHolder inventoryHolder)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _inventoryHolder = inventoryHolder ?? throw new ArgumentNullException(nameof(inventoryHolder));
        _texts = new PluginLocalizer(_context.Localization, typeof(ModuleToolViewModel).Assembly, ResourceBaseName);
        _texts.CultureChanged += Texts_CultureChanged;
        _config = LoadConfig();
        _effectIndexById = _catalog.Effects
            .Select((effect, index) => (effect.EffectId, index))
            .ToDictionary(pair => pair.EffectId, pair => pair.index);

        _elapsedTimer = new DispatcherTimer { Interval = ElapsedRefreshInterval };
        _elapsedTimer.Tick += (_, _) => OnPropertyChanged(nameof(ResultsSummaryText));

        SearchCommand = new RelayCommand(StartOrCancelSearch, CanStartOrCancelSearch);
        ResetCommand = new RelayCommand(ResetConditions);

        foreach (var quality in _catalog.Qualities)
        {
            QualityToggles.Add(new ModuleQualityToggleViewModel(quality, GetQualityLabel(quality), true, OnQualitiesChanged));
        }

        ApplyConditions(_config);
        _shownConditions = GetConditions();
        _inventoryHolder.InventoryChanged += InventoryHolder_InventoryChanged;
        _inventoryHolder.ReceivedAgain += InventoryHolder_ReceivedAgain;
        ApplyInventory(isUpdate: false);
    }

    public PluginLocalizer Texts => _texts;

    public RelayCommand SearchCommand { get; }

    public RelayCommand ResetCommand { get; }

    public ObservableCollection<ModulePriorityEffectRowViewModel> PriorityEffects { get; } = [];

    public ObservableCollection<ModuleQualityToggleViewModel> QualityToggles { get; } = [];

    public ObservableCollection<ModuleResultCardViewModel> Cards { get; } = [];

    public bool IsInventoryLoaded => _inventory is not null;

    public string InventorySummaryText => _inventory is null
        ? string.Empty
        : _texts.Format("Module.Inventory.Summary", _inventory.Modules.Count, _inventory.EquippedUuidBySlot.Count, _catalog.SlotCount);

    public string EquippedFightText => _equipped is not null
        ? _texts.Format("Module.Inventory.EquippedFight", _equipped.FightValue)
        : _texts["Module.Inventory.EquippedFightUnavailable"];

    public bool HasEquippedFight => _inventory is not null && _inventory.EquippedUuidBySlot.Count > 0;

    public string ReceivedAtText => _receivedAt is { } receivedAt
        ? _texts.Format("Module.Inventory.ReceivedAt", receivedAt.ToString("HH:mm", _texts.CurrentCulture))
        : string.Empty;

    public bool HasReceivedAt => _receivedAt is not null && _inventory is not null;

    public string MissingLinksText => _texts.Format("Module.Inventory.MissingLinks", _missingLinkModuleCount);

    public bool HasMissingLinks => _inventory is not null && _missingLinkModuleCount > 0;

    public string UnknownModulesText => _texts.Format("Module.Inventory.UnknownModules", _unknownModuleCount);

    public bool HasUnknownModules => _inventory is not null && _unknownModuleCount > 0;

    public IReadOnlyList<ModuleEffectOption> AddEffectOptions
    {
        get => _addEffectOptions;
        private set => SetProperty(ref _addEffectOptions, value);
    }

    public bool CanAddEffect => _addEffectOptions.Count > 0;

    public bool IsSearching => _search is not null;

    public string SearchButtonText => IsSearching ? _texts["Module.Button.Cancel"] : _texts["Module.Button.Search"];

    public bool HasResults => _results is not null;

    public string ResultsTitleText => IsSearching ? _texts["Module.Section.Searching"] : _texts["Module.Section.Results"];

    /// <summary>検索中は進み具合と経過、検索の後は件数(11件以上なら全体の件数と上位の件数)と秒数。</summary>
    public string ResultsSummaryText
    {
        get
        {
            var culture = _texts.CurrentCulture;
            if (IsSearching)
            {
                return _texts.Format(
                    "Module.Search.Progress",
                    Math.Floor(_progress * 100).ToString("0", culture),
                    _stopwatch.Elapsed.TotalSeconds.ToString("0.000", culture));
            }

            if (_results is null)
            {
                return string.Empty;
            }

            var seconds = _resultElapsedSeconds.ToString("0.000", culture);
            return _resultMatchingCount > _results.Count
                ? _texts.Format("Module.Results.Summary", _resultMatchingCount, _results.Count, seconds)
                : _texts.Format("Module.Results.Count", _results.Count, seconds);
        }
    }

    /// <summary>
    /// 検索中は走っている検索の条件、検索の後は表示している結果の条件。
    /// その検索を始めた後に所持モジュールが変わったら、検索し直す知らせを後ろに付ける。
    /// </summary>
    public string SearchConditionsText
    {
        get
        {
            IReadOnlyList<ModulePriorityEffect> conditions;
            bool inventoryChanged;
            if (IsSearching)
            {
                conditions = _runningConditions;
                inventoryChanged = _runningInventoryChanged;
            }
            else if (_results is not null)
            {
                conditions = _shownConditions;
                inventoryChanged = _resultsInventoryChanged;
            }
            else
            {
                return string.Empty;
            }

            var text = string.Join(
                ConditionSeparator,
                conditions.Select(condition => _texts.Format(
                    "Module.Conditions.Effect",
                    _catalog.GetEffectName(condition.EffectId, _texts.CurrentCulture),
                    condition.MinLevel)));
            if (!inventoryChanged)
            {
                return text;
            }

            var note = _texts["Module.Results.InventoryChanged"];
            return text.Length == 0 ? note : _texts.Format("Module.Results.ConditionsWithNote", text, note);
        }
    }

    public bool HasSearchConditionsText => SearchConditionsText.Length > 0;

    /// <summary>効果を追加する選択リストで選ばれた効果を、優先する効果の最後に足す。</summary>
    public void AddPriorityEffect(int effectId)
    {
        if (PriorityEffects.Any(row => row.EffectId == effectId))
        {
            return;
        }

        PriorityEffects.Add(CreatePriorityRow(effectId, 1));
        RebuildAddEffectOptions();
        OnConditionsChanged();
    }

    /// <summary>開くときに読めなかった設定ファイルを、画面が出たところで知らせる。</summary>
    internal void ShowPendingSettingsLoadFailedMessage()
    {
        if (!_pendingSettingsLoadFailedMessage)
        {
            return;
        }

        _pendingSettingsLoadFailedMessage = false;
        _context.Messages.Show(
            _texts["Module.Message.SettingsLoadError.Title"],
            _texts["Module.Message.SettingsLoadError.Message"],
            _texts.Format("Module.Message.SettingsLoadError.Detail", _context.Settings.GetFilePath(ConfigFileName)));
    }

    /// <summary>窓を閉じたとき。所持モジュールの知らせの購読を外し、検索を止める。</summary>
    public void Dispose()
    {
        _inventoryHolder.InventoryChanged -= InventoryHolder_InventoryChanged;
        _inventoryHolder.ReceivedAgain -= InventoryHolder_ReceivedAgain;
        _texts.CultureChanged -= Texts_CultureChanged;
        _texts.Dispose();
        _elapsedTimer.Stop();
        _search?.Cancel();
    }

    private void InventoryHolder_InventoryChanged(object? sender, EventArgs e)
    {
        ApplyInventory(isUpdate: true);
    }

    // 中身が同じなので、カード・検索の印は変えず、受け取った時刻だけを出し直す。
    private void InventoryHolder_ReceivedAgain(object? sender, EventArgs e)
    {
        _receivedAt = _inventoryHolder.ReceivedAt;
        OnPropertyChanged(nameof(ReceivedAtText));
        OnPropertyChanged(nameof(HasReceivedAt));
    }

    private void Texts_CultureChanged(object? sender, EventArgs e)
    {
        foreach (var toggle in QualityToggles)
        {
            toggle.Label = GetQualityLabel(toggle.Quality);
        }

        var levelOptions = CreateLevelOptions();
        foreach (var row in PriorityEffects)
        {
            row.Relabel(_catalog.GetEffectName(row.EffectId, _texts.CurrentCulture), levelOptions);
        }

        RebuildAddEffectOptions();
        RebuildCards();
        RaiseInventoryChanged();
        RaiseSearchChanged();
    }

    private void ApplyInventory(bool isUpdate)
    {
        var inventory = _inventoryHolder.Inventory;
        _inventory = inventory;
        _receivedAt = _inventoryHolder.ReceivedAt;
        _missingLinkModuleCount = 0;
        _unknownModuleCount = 0;

        var usable = new List<ModuleCandidate>();
        foreach (var module in inventory?.Modules ?? [])
        {
            if (module.Effects.Any(effect => effect.LinkCount is null))
            {
                _missingLinkModuleCount++;
                continue;
            }

            if (!_catalog.TryGetModule(module.ConfigId, out _)
                || module.Effects.Any(effect => !_effectIndexById.ContainsKey(effect.EffectId)))
            {
                _unknownModuleCount++;
                if (_reportedUnknownModules.Add(module.Uuid))
                {
                    _context.Logger.Warning(
                        $"Module {module.Uuid} (config {module.ConfigId}, effects {string.Join(",", module.Effects.Select(effect => effect.EffectId))}) is not in the module tables; it is excluded.");
                }

                continue;
            }

            usable.Add(new ModuleCandidate(
                module.Uuid,
                module.ConfigId,
                module.Effects.Select(effect => (_effectIndexById[effect.EffectId], effect.LinkCount!.Value)).ToArray()));
        }

        _usableModules = usable;
        _equipped = CreateEquippedCombination();
        if (isUpdate)
        {
            _resultsInventoryChanged |= _results is not null;
            _runningInventoryChanged |= IsSearching;
        }

        RebuildCards();
        RaiseInventoryChanged();
        RaiseSearchChanged();
    }

    private ModuleCombination? CreateEquippedCombination()
    {
        if (_inventory is null || _inventory.EquippedUuidBySlot.Count == 0)
        {
            return null;
        }

        var byUuid = _usableModules.ToDictionary(module => module.Uuid);
        var modules = new List<ModuleCandidate>();
        foreach (var (_, uuid) in _inventory.EquippedUuidBySlot.OrderBy(pair => pair.Key))
        {
            if (!byUuid.TryGetValue(uuid, out var module))
            {
                return null;
            }

            modules.Add(module);
        }

        var scorer = CreateScorer([]);
        var links = SumLinks(modules);
        var totalLinks = modules.Sum(module => module.TotalLinks);
        return new ModuleCombination(scorer.GetFightValue(links, totalLinks), totalLinks, modules);
    }

    private IReadOnlyList<ModuleCandidate> GetCandidates()
    {
        var excluded = QualityToggles.Where(toggle => !toggle.IsSelected).Select(toggle => toggle.Quality).ToHashSet();
        return _usableModules
            .Where(module => _catalog.TryGetModule(module.ConfigId, out var definition) && !excluded.Contains(definition.Quality))
            .ToArray();
    }

    private ModuleScorer CreateScorer(IReadOnlyList<ModulePriorityEffect> conditions)
    {
        return new ModuleScorer(
            _catalog.Effects,
            _catalog.GetLinkFightValues(_context.GameData.CurrentServer),
            conditions);
    }

    private IReadOnlyList<ModulePriorityEffect> GetConditions()
    {
        return PriorityEffects.Select(row => new ModulePriorityEffect(row.EffectId, row.MinLevel)).ToArray();
    }

    private bool CanStartOrCancelSearch()
    {
        return IsSearching || (_inventory is not null && GetCandidates().Count > 0);
    }

    private async void StartOrCancelSearch()
    {
        if (_search is not null)
        {
            _search.Cancel();
            return;
        }

        var candidates = GetCandidates();
        var equippedUuids = _inventory?.EquippedUuidBySlot.Values.ToHashSet() ?? [];
        var conditions = GetConditions();
        using var search = new CancellationTokenSource();
        _search = search;
        _runningConditions = conditions;
        _runningInventoryChanged = false;
        _progress = 0;
        _stopwatch.Restart();
        _elapsedTimer.Start();
        RaiseSearchChanged();

        var progress = new Progress<double>(value =>
        {
            _progress = value;
            OnPropertyChanged(nameof(ResultsSummaryText));
        });

        try
        {
            var scorer = CreateScorer(conditions);
            var result = await Task.Run(
                () => new ModuleSolver(scorer, candidates, equippedUuids, _catalog.SlotCount, ResultCount)
                    .Solve(progress, search.Token));
            _stopwatch.Stop();

            // null はキャンセル。前の結果をそのまま残す。
            if (result is not null)
            {
                _results = result.Combinations;
                _resultMatchingCount = result.MatchingCount;
                _resultElapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
                _shownConditions = conditions;
                _resultsInventoryChanged = _runningInventoryChanged;
                RebuildCards();
                OnPropertyChanged(nameof(HasResults));
            }
        }
        catch (Exception exception)
        {
            _context.Logger.Error("Module combination search failed.", exception);
            _context.Messages.Show(
                _texts["Module.Message.SearchError.Title"],
                _texts["Module.Message.SearchError.Message"],
                _texts["Module.Message.SearchError.Detail"]);
        }
        finally
        {
            _stopwatch.Stop();
            _elapsedTimer.Stop();
            _search = null;
            _runningConditions = [];
            RaiseSearchChanged();
        }
    }

    // 条件と品質を変えても、結果とカードは次の検索まで変えない(保存と、検索ボタンが押せるかだけ)。
    private void OnConditionsChanged()
    {
        SaveConfig();
        SearchCommand.RaiseCanExecuteChanged();
    }

    /// <summary>条件と品質を、新しく作った設定ファイルと同じ状態に戻す。</summary>
    private void ResetConditions()
    {
        ApplyConditions(new ModuleToolConfig());
        OnConditionsChanged();
    }

    /// <summary>設定の条件と品質を画面へ当てる。開いたときとリセットで使う。品質の変化の知らせ(保存)は出さない。</summary>
    private void ApplyConditions(ModuleToolConfig config)
    {
        PriorityEffects.Clear();
        foreach (var priority in config.PriorityEffects)
        {
            if (_effectIndexById.ContainsKey(priority.EffectId) && PriorityEffects.All(row => row.EffectId != priority.EffectId))
            {
                PriorityEffects.Add(CreatePriorityRow(priority.EffectId, priority.MinLevel));
            }
            else
            {
                _context.Logger.Warning($"Ignored a saved priority effect {priority.EffectId} that is unknown or duplicated.");
            }
        }

        foreach (var toggle in QualityToggles)
        {
            toggle.SetSelected(!config.ExcludedQualities.Contains(toggle.Quality));
        }

        RebuildAddEffectOptions();
    }

    private void OnQualitiesChanged()
    {
        OnConditionsChanged();
    }

    private void RemovePriorityEffect(ModulePriorityEffectRowViewModel row)
    {
        if (PriorityEffects.Remove(row))
        {
            RebuildAddEffectOptions();
            OnConditionsChanged();
        }
    }

    private ModulePriorityEffectRowViewModel CreatePriorityRow(int effectId, int minLevel)
    {
        return new ModulePriorityEffectRowViewModel(
            effectId,
            _catalog.GetEffectName(effectId, _texts.CurrentCulture),
            _catalog.GetImage(_catalog.Effects[_effectIndexById[effectId]].Icon),
            CreateLevelOptions(),
            minLevel,
            OnConditionsChanged,
            RemovePriorityEffect);
    }

    // 選んだ段階以上が条件。Lv1 はその効果が入っていること。
    private IReadOnlyList<ModuleOption<int>> CreateLevelOptions()
    {
        var maxLevel = _catalog.Effects.Max(effect => effect.MaxLevel);
        return Enumerable.Range(1, maxLevel)
            .Select(level => new ModuleOption<int>(level, _texts.Format("Module.Level", level)))
            .ToArray();
    }

    private void RebuildAddEffectOptions()
    {
        AddEffectOptions = _catalog.Effects
            .Where(effect => PriorityEffects.All(row => row.EffectId != effect.EffectId))
            .Select(effect => new ModuleEffectOption(
                effect.EffectId,
                _catalog.GetEffectName(effect.EffectId, _texts.CurrentCulture),
                _catalog.GetImage(effect.Icon)))
            .ToArray();
        OnPropertyChanged(nameof(CanAddEffect));
    }

    private void RebuildCards()
    {
        Cards.Clear();
        if (_inventory is null)
        {
            return;
        }

        var scorer = CreateScorer([]);
        var priorityOrder = _shownConditions
            .Select((condition, index) => (condition.EffectId, index))
            .ToDictionary(pair => _effectIndexById[pair.EffectId], pair => pair.index);
        var equippedUuids = _inventory.EquippedUuidBySlot.Values.ToHashSet();

        if (_equipped is not null)
        {
            Cards.Add(CreateCard(_texts["Module.Card.Equipped"], true, _equipped, scorer, priorityOrder, equippedUuids));
        }

        var rank = 1;
        foreach (var result in _results ?? [])
        {
            Cards.Add(CreateCard(_texts.Format("Module.Card.Rank", rank++), false, result, scorer, priorityOrder, equippedUuids));
        }
    }

    private ModuleResultCardViewModel CreateCard(
        string title,
        bool isEquippedCard,
        ModuleCombination combination,
        ModuleScorer scorer,
        IReadOnlyDictionary<int, int> priorityOrder,
        IReadOnlySet<long> equippedUuids)
    {
        var culture = _texts.CurrentCulture;
        var links = SumLinks(combination.Modules);
        var chips = Enumerable.Range(0, links.Length)
            .Where(effectIndex => links[effectIndex] > 0)
            .Select(effectIndex => (EffectIndex: effectIndex, Links: links[effectIndex], Level: scorer.GetLevel(effectIndex, links[effectIndex])))
            .OrderBy(effect => priorityOrder.TryGetValue(effect.EffectIndex, out var order) ? order : int.MaxValue)
            .ThenByDescending(effect => effect.Level)
            .ThenByDescending(effect => effect.Links)
            .ThenBy(effect => _catalog.Effects[effect.EffectIndex].EffectId)
            .Select(effect =>
            {
                var definition = _catalog.Effects[effect.EffectIndex];
                return new ModuleEffectChipViewModel(
                    _catalog.GetImage(definition.Icon),
                    _catalog.GetEffectName(definition.EffectId, culture),
                    effect.Links,
                    effect.Level,
                    _texts.Format("Module.Level", effect.Level));
            })
            .ToArray();

        var tiles = combination.Modules
            .Select(module =>
            {
                _catalog.TryGetModule(module.ConfigId, out var definition);
                return new ModuleTileViewModel(
                    _catalog.GetQualityImage(definition.Quality),
                    _catalog.GetImage(definition.Icon),
                    _catalog.GetModuleName(module.ConfigId, culture),
                    equippedUuids.Contains(module.Uuid),
                    module.Effects
                        .Select(effect =>
                        {
                            var effectDefinition = _catalog.Effects[effect.EffectIndex];
                            return new ModulePartViewModel(
                                _catalog.GetImage(effectDefinition.Icon),
                                effect.Links,
                                _texts.Format(
                                    "Module.Tile.PartTip",
                                    _catalog.GetEffectName(effectDefinition.EffectId, culture),
                                    effect.Links));
                        })
                        .ToArray());
            })
            .ToArray();

        var delta = !isEquippedCard && _equipped is not null ? combination.FightValue - _equipped.FightValue : (int?)null;
        return new ModuleResultCardViewModel(
            isEquippedCard,
            title,
            _texts["Module.Card.FightValue"],
            combination.FightValue.ToString(culture),
            delta is { } value ? FormatDelta(value) : null,
            delta ?? 0,
            _texts.Format("Module.Card.LinkTotal", combination.TotalLinks),
            chips,
            tiles);
    }

    private int[] SumLinks(IEnumerable<ModuleCandidate> modules)
    {
        var links = new int[_catalog.Effects.Count];
        foreach (var module in modules)
        {
            foreach (var (effectIndex, effectLinks) in module.Effects)
            {
                links[effectIndex] += effectLinks;
            }
        }

        return links;
    }

    // 読めない設定ファイルは使わずに初期値で開き、画面が出たところで知らせる(ModuleToolView の Loaded)。
    private ModuleToolConfig LoadConfig()
    {
        try
        {
            return _context.Settings.Load<ModuleToolConfig>(ConfigFileName);
        }
        catch (InvalidDataException)
        {
            _pendingSettingsLoadFailedMessage = true;
            return new ModuleToolConfig();
        }
    }

    private void SaveConfig()
    {
        _config.PriorityEffects = PriorityEffects
            .Select(row => new ModulePriorityEffectConfig { EffectId = row.EffectId, MinLevel = row.MinLevel })
            .ToList();
        _config.ExcludedQualities = QualityToggles.Where(toggle => !toggle.IsSelected).Select(toggle => toggle.Quality).ToList();

        try
        {
            _context.Settings.Save(ConfigFileName, _config);
        }
        catch (Exception exception)
        {
            _context.Logger.Error("Failed to save the module tool settings.", exception);
            if (!_hasReportedSaveFailure)
            {
                _hasReportedSaveFailure = true;
                _context.Messages.Show(
                    _texts["Module.Message.SaveError.Title"],
                    _texts["Module.Message.SaveError.Message"],
                    _context.Settings.GetFilePath(ConfigFileName));
            }
        }
    }

    private string GetQualityLabel(int quality)
    {
        return _texts[string.Create(CultureInfo.InvariantCulture, $"Module.Quality.{quality}")];
    }

    private string FormatDelta(int delta)
    {
        return delta.ToString("+0;-0;±0", _texts.CurrentCulture);
    }

    private void RaiseInventoryChanged()
    {
        OnPropertyChanged(nameof(IsInventoryLoaded));
        OnPropertyChanged(nameof(InventorySummaryText));
        OnPropertyChanged(nameof(EquippedFightText));
        OnPropertyChanged(nameof(HasEquippedFight));
        OnPropertyChanged(nameof(ReceivedAtText));
        OnPropertyChanged(nameof(HasReceivedAt));
        OnPropertyChanged(nameof(MissingLinksText));
        OnPropertyChanged(nameof(HasMissingLinks));
        OnPropertyChanged(nameof(UnknownModulesText));
        OnPropertyChanged(nameof(HasUnknownModules));
    }

    private void RaiseSearchChanged()
    {
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(SearchButtonText));
        OnPropertyChanged(nameof(ResultsTitleText));
        OnPropertyChanged(nameof(ResultsSummaryText));
        OnPropertyChanged(nameof(SearchConditionsText));
        OnPropertyChanged(nameof(HasSearchConditionsText));
        SearchCommand.RaiseCanExecuteChanged();
    }
}
