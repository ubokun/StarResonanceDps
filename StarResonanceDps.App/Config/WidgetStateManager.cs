// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Text.Json;
using StarResonanceDps.App.Models.Widgets;

namespace StarResonanceDps.App.Config;

public sealed class WidgetStateManager
{
    private static readonly Lazy<WidgetStateManager> LazyInstance = new(() => new WidgetStateManager());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _statePath;
    private readonly object _syncRoot = new();
    private WidgetStateDocument _document;

    private WidgetStateManager()
    {
        _statePath = AppDataPaths.WidgetStatePath;
        _document = Load();
    }

    public static WidgetStateManager Instance => LazyInstance.Value;

    /// <summary>起動時に、ファイルはあるのに読めなかった(壊れている・ウィジェットの設定が入っていない)か。既定値で動いている。</summary>
    public bool HasLoadFailed { get; private set; }

    public WidgetConfig GetWidgetSnapshot(WidgetKey key)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(key);
            return WidgetConfigDefaults.CloneNormalized(key.Kind, config);
        }
    }

    public void SaveWidget(WidgetKey key, WidgetConfig config)
    {
        lock (_syncRoot)
        {
            _document.Widgets[key.DocumentKey] = WidgetConfigDefaults.CloneNormalized(key.Kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetFlags(WidgetKey key, bool isFavorite, bool isPinned, bool isClickThrough)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(key);
            config.IsFavorite = isFavorite;
            config.IsPinned = isPinned;
            config.IsClickThrough = isClickThrough;
            WidgetConfigDefaults.Normalize(key.Kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetState(WidgetKey key, WidgetState state)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(key);
            config.State = state;
            WidgetConfigDefaults.Normalize(key.Kind, config);
            SaveCore();
        }
    }

    public void SaveWidgetTheme(WidgetKey key, WidgetThemeConfig theme)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(key);
            config.Theme = WidgetConfigDefaults.CloneNormalizedTheme(theme);
            WidgetConfigDefaults.Normalize(key.Kind, config);
            SaveCore();
        }
    }

    /// <param name="width">幅。保存しない辺(<see cref="WidgetConfigDefaults.GetFixedWindowSize"/>)は null で、読み込みの正規化が既定の大きさで埋める。</param>
    /// <param name="height">高さ。幅と同じ。</param>
    public void SaveWidgetWindowBounds(WidgetKey key, double x, double y, double? width, double? height)
    {
        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(key);
            config.Window = new WidgetWindowConfig
            {
                X = x,
                Y = y,
                Width = width,
                Height = height
            };
            WidgetConfigDefaults.Normalize(key.Kind, config);
            SaveCore();
        }
    }

    /// <summary>プラグインのウィジェットの保存を全部消す(プラグインをアンインストールしたとき)。無ければ何もしない。</summary>
    public void RemovePluginWidgets(string pluginKey)
    {
        lock (_syncRoot)
        {
            var removedKeys = _document.Widgets.Keys
                .Where(key => WidgetKey.IsPluginDocumentKeyOf(key, pluginKey))
                .ToArray();
            if (removedKeys.Length == 0)
            {
                return;
            }

            foreach (var key in removedKeys)
            {
                _document.Widgets.Remove(key);
            }

            SaveCore();
        }
    }

    /// <summary>
    /// 開いていた窓の対象一覧を保存する。窓が増減したときだけ呼ぶ。
    /// </summary>
    public void SaveWidgetOpenTargets(WidgetKey key, IReadOnlyList<WidgetOpenTargetConfig> targets)
    {
        if (!key.IsPlugin && !WidgetConfigDefaults.SupportsOpenTargets(key.Kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(key);
            config.OpenTargets = targets.Count == 0
                ? null
                : targets.Select(target => target.Clone()).ToList();
            WidgetConfigDefaults.Normalize(key.Kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// ステータス詳細の行の並びだけを保存する。設定の窓が開いていないときの、ウィジェットでの並べ替えが使う。
    ///
    /// <para>
    /// ウィンドウの位置と同じく、設定一式は書き戻さずにこの項目だけを書く。
    /// </para>
    /// </summary>
    public void SavePlayerStatusRowOrder(WidgetKind kind, IReadOnlyList<int> order)
    {
        if (kind != WidgetKind.PlayerStatus)
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.PlayerStatusRowOrder = [.. order];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフカードの倍率を1件だけ保存する。
    ///
    /// <para>
    /// 拡大縮小はウィンドウごとに独立していて、他のウィンドウの値を巻き込みたくない。
    /// 設定一式を書き戻す形にすると、開いている別のカードが持っている値を上書きしてしまう。
    /// </para>
    /// </summary>
    public void SaveBuffCardScale(WidgetKind kind, string scaleKey, int scale)
    {
        if (string.IsNullOrWhiteSpace(scaleKey))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffCard ??= WidgetConfigDefaults.CreateBuffCardSettings();
            config.BuffCard.Scales[scaleKey] = WidgetConfigDefaults.ClampBuffCardScale(scale);
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフ一覧の非表示のバフだけを保存する。窓の右クリックで非表示にしたときに呼ぶ。
    ///
    /// <para>
    /// 設定一式を書き戻す形にすると、開いている設定ウィンドウが持っている未保存の値とぶつかる。
    /// ステータス詳細の行の並びと同じく、この項目だけを書く。
    /// </para>
    /// </summary>
    public void SaveBuffListHiddenBuffs(WidgetKind kind, IReadOnlyList<int> hiddenBuffIds)
    {
        if (!WidgetConfigDefaults.SupportsBuffListSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffList ??= WidgetConfigDefaults.CreateBuffListSettings(kind);
            config.BuffList.HiddenBuffIds = [.. hiddenBuffIds];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>エンティティ情報の非表示のバフだけを保存する。枠の右クリックで非表示にしたときに呼ぶ(バフ・デバフ一覧と同じく、この項目だけを書く)。</summary>
    public void SaveEntityInfoHiddenBuffs(WidgetKind kind, IReadOnlyList<int> hiddenBuffIds)
    {
        if (!WidgetConfigDefaults.SupportsEntityInfoSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.EntityInfo ??= WidgetConfigDefaults.CreateEntityInfoSettings();
            config.EntityInfo.HiddenBuffIds = [.. hiddenBuffIds];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフ一覧の優先表示リストだけを保存する。窓の右クリックで優先表示にした・やめたときに呼ぶ(非表示のバフと同じく、この項目だけを書く)。
    /// </summary>
    public void SaveBuffListPriorityBuffs(WidgetKind kind, IReadOnlyList<BuffDebuffGroupEntryConfig> priorityBuffs)
    {
        if (!WidgetConfigDefaults.SupportsBuffListSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffList ??= WidgetConfigDefaults.CreateBuffListSettings(kind);
            config.BuffList.PriorityBuffs = [.. priorityBuffs.Select(entry => entry.Clone())];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>エンティティ情報の優先表示リストだけを保存する。枠の右クリックで優先表示にした・やめたときに呼ぶ(バフ・デバフ一覧と同じく、この項目だけを書く)。</summary>
    public void SaveEntityInfoPriorityBuffs(WidgetKind kind, IReadOnlyList<BuffDebuffGroupEntryConfig> priorityBuffs)
    {
        if (!WidgetConfigDefaults.SupportsEntityInfoSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.EntityInfo ??= WidgetConfigDefaults.CreateEntityInfoSettings();
            config.EntityInfo.PriorityBuffs = [.. priorityBuffs.Select(entry => entry.Clone())];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフグループの行だけを保存する。バフ・デバフ一覧かカードの右クリックで行を足したときに呼ぶ
    /// (バフ・デバフ一覧の非表示のバフと同じく、この項目だけを書く)。
    /// </summary>
    public void SaveBuffDebuffGroupEntries(WidgetKind kind, IReadOnlyList<BuffDebuffGroupEntryConfig> entries)
    {
        if (!WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffDebuffGroup ??= WidgetConfigDefaults.CreateBuffDebuffGroupSettings();
            config.BuffDebuffGroup.Entries = [.. entries.Select(entry => entry.Clone())];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフカードのショートカットだけを保存する。バフ・デバフ一覧かカードの右クリックで行を足したときに呼ぶ
    /// (バフ・デバフグループの行と同じく、この項目だけを書く)。
    /// </summary>
    public void SaveBuffCardShortcuts(WidgetKind kind, IReadOnlyList<BuffDebuffGroupEntryConfig> shortcuts)
    {
        if (!WidgetConfigDefaults.SupportsBuffCardSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffCard ??= WidgetConfigDefaults.CreateBuffCardSettings();
            config.BuffCard.Shortcuts = [.. shortcuts.Select(entry => entry.Clone())];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフカードの通知対象だけを保存する。カードの右クリックで通知対象にした・しないときに呼ぶ(ショートカットと同じく、この項目だけを書く)。
    /// </summary>
    public void SaveBuffCardNotificationTargets(WidgetKind kind, IReadOnlyList<BuffDebuffGroupEntryConfig> notificationTargets)
    {
        if (!WidgetConfigDefaults.SupportsBuffCardSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffCard ??= WidgetConfigDefaults.CreateBuffCardSettings();
            config.BuffCard.NotificationTargets = [.. notificationTargets.Select(entry => entry.Clone())];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// バフ・デバフグループの通知対象だけを保存する。グループの行の右クリックで通知対象にした・しないときに呼ぶ(グループの行と同じく、この項目だけを書く)。
    /// </summary>
    public void SaveBuffDebuffGroupNotificationTargets(WidgetKind kind, IReadOnlyList<BuffDebuffGroupEntryConfig> notificationTargets)
    {
        if (!WidgetConfigDefaults.SupportsBuffDebuffGroupSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.BuffDebuffGroup ??= WidgetConfigDefaults.CreateBuffDebuffGroupSettings();
            config.BuffDebuffGroup.NotificationTargets = [.. notificationTargets.Select(entry => entry.Clone())];
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// 推移グラフの横軸の長さだけを保存する。窓の上のホイールで変えて、ホイールが止まったときに呼ぶ。
    ///
    /// <para>
    /// 設定一式を書き戻す形にすると、開いている設定ウィンドウが持っている未保存の値とぶつかる。
    /// バフ・デバフ一覧の非表示のバフと同じく、この項目だけを書く。
    /// </para>
    /// </summary>
    public void SaveMetricGraphVisibleSeconds(WidgetKind kind, int visibleSeconds)
    {
        if (!WidgetConfigDefaults.SupportsMetricGraphSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.MetricGraph ??= WidgetConfigDefaults.CreateMetricGraphSettings(kind);
            config.MetricGraph.VisibleSeconds = visibleSeconds;
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    /// <summary>
    /// 被ダメログの「最新の被ダメのみ表示」だけを保存する。ヘッダーのボタンで切り替えたときに呼ぶ。
    /// 横軸の長さ(<see cref="SaveMetricGraphVisibleSeconds"/>)と同じく、開いている設定ウィンドウの未保存の値とぶつからないよう、この項目だけを書く。
    /// </summary>
    public void SaveDamageTakenLogShowsLatestHitOnly(WidgetKind kind, bool showsLatestHitOnly)
    {
        if (!WidgetConfigDefaults.SupportsDamageTakenLogSettings(kind))
        {
            return;
        }

        lock (_syncRoot)
        {
            var config = GetOrCreateWidgetConfig(kind);
            config.DamageTakenLog ??= WidgetConfigDefaults.CreateDamageTakenLogSettings();
            config.DamageTakenLog.ShowsLatestHitOnly = showsLatestHitOnly;
            WidgetConfigDefaults.Normalize(kind, config);
            SaveCore();
        }
    }

    private WidgetConfig GetOrCreateWidgetConfig(WidgetKey key)
    {
        if (_document.Widgets.TryGetValue(key.DocumentKey, out var config))
        {
            return config;
        }

        config = WidgetConfigDefaults.Create(key.Kind);
        _document.Widgets[key.DocumentKey] = config;
        return config;
    }

    private WidgetStateDocument Load()
    {
        if (!File.Exists(_statePath))
        {
            return CreateDefaultDocument();
        }

        try
        {
            var json = File.ReadAllText(_statePath);
            var document = JsonSerializer.Deserialize<WidgetStateDocument>(json, JsonOptions);
            if (document?.Widgets is null)
            {
                HasLoadFailed = true;
                return CreateDefaultDocument();
            }

            Normalize(document);
            return document;
        }
        catch
        {
            // 読めないファイルは丸ごと既定値で動く。メイン窓が出たところで知らせ(SettingsLoadFailureMessage)、次の保存で上書きする。
            HasLoadFailed = true;
            return CreateDefaultDocument();
        }
    }

    private static WidgetStateDocument CreateDefaultDocument()
    {
        var document = new WidgetStateDocument();
        Normalize(document);
        return document;
    }

    /// <summary>
    /// 本体の種別は全部そろえる(無ければ既定で足す)。プラグインのウィジェットは、どのプラグインのものかは読み込みの後でないと分からないので、
    /// あるものだけを正規化して残す(アンインストールで消す、<see cref="RemovePluginWidgets"/>)。
    /// </summary>
    private static void Normalize(WidgetStateDocument document)
    {
        document.Widgets ??= new Dictionary<string, WidgetConfig>(StringComparer.OrdinalIgnoreCase);

        // Migrate the retired custom meter once. An existing native meter wins.
        if (document.Widgets.Remove("HatePs", out var legacy) && legacy is not null
            && !document.Widgets.ContainsKey("HateMeter"))
            document.Widgets["HateMeter"] = legacy;

        foreach (WidgetKind kind in Enum.GetValues<WidgetKind>())
        {
            if (kind is WidgetKind.Plugin or WidgetKind.HatePs)
            {
                continue;
            }

            WidgetKey key = kind;
            if (!document.Widgets.TryGetValue(key.DocumentKey, out var config))
            {
                document.Widgets[key.DocumentKey] = WidgetConfigDefaults.Create(kind);
                continue;
            }

            WidgetConfigDefaults.Normalize(kind, config);
        }

        foreach (var (documentKey, config) in document.Widgets.ToArray())
        {
            if (!WidgetKey.IsPluginDocumentKey(documentKey))
            {
                continue;
            }

            if (config is null)
            {
                document.Widgets.Remove(documentKey);
                continue;
            }

            WidgetConfigDefaults.Normalize(WidgetKind.Plugin, config);
        }
    }

    private void SaveCore()
    {
        Normalize(_document);

        var directory = Path.GetDirectoryName(_statePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_document, JsonOptions);
        File.WriteAllText(_statePath, json);
    }
}
