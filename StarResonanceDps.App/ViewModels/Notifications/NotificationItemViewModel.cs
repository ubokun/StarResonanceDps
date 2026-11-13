// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Notifications;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels.Plugins;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.App.ViewModels.Notifications;

/// <summary>
/// 通知タブの1項目。一覧の行(名前・スイッチ)と、選んだときに右に出す文章と設定(項目だけの設定・出し方・音量・声)を持つ。
/// 値が変わったら <see cref="Changed"/>(文章は <see cref="TextChanged"/>)で通知タブへ知らせ、通知タブが保存する。
///
/// <para>
/// 本体の項目(<see cref="Kind"/>)とプラグインの項目(<see cref="PluginItemKey"/>)がある。プラグインの項目は、項目だけの設定の代わりに
/// プラグインが定義した設定(<see cref="PluginSettings"/>)を持ち、プラグインの最初の項目の上にプラグインの名前の見出し(<see cref="GroupHeader"/>)を出す。
/// </para>
/// </summary>
public sealed partial class NotificationItemViewModel : ObservableObject
{
    private readonly string? _labelResourceKey;
    private readonly PluginNotificationGroup? _pluginGroup;
    private readonly PluginNotificationDefinition? _pluginDefinition;
    private readonly bool _isFirstInPluginGroup;
    private bool _isLoading;

    // 選んである VOICEVOX の話者(保存する値)。選択肢を作り直すときに選択リストが null を書き戻しても、ここは変えない。
    private string _voicevoxSpeakerUuid = string.Empty;
    private string _voicevoxSpeakerName = string.Empty;
    private int? _voicevoxStyleId;
    private string _voicevoxStyleName = string.Empty;

    private readonly ObservableCollection<VoicevoxSpeakerOption> _voicevoxSpeakerOptions = [];

    private readonly ObservableCollection<VoicevoxStyleOption> _voicevoxStyleOptions = [];

    /// <summary>最後にエンジンから取れた話者の一覧(通知タブが全部の項目に配る)。まだ問い合わせていなければ null。</summary>
    private IReadOnlyList<VoicevoxStyle>? _fetchedVoicevoxStyles;

    private bool _isRebuildingVoicevoxOptions;

    /// <summary>スタイルの行が要るか(<see cref="RebuildVoicevoxStyleOptions"/> が決める)。</summary>
    private bool _isVoicevoxStyleRowNeeded;

    [ObservableProperty]
    private string _label = string.Empty;

    /// <summary>行の上の見出し(プラグインの名前)。プラグインの最初の項目だけで、ほかは空。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasGroupHeader))]
    private string _groupHeader = string.Empty;

    /// <summary>一覧で選んでいる行か。選んだ行の文章と設定が右に出る。</summary>
    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEnabled;

    [ObservableProperty]
    private int _methodIndex = AppConfigDefaults.NotificationMethodWindowsIndex;

    /// <summary>通知音量。スライダーの値なので double で持ち、保存のときに整数へ丸める。</summary>
    [ObservableProperty]
    private double _volume = AppConfigDefaults.NotificationVolumeDefault;

    [ObservableProperty]
    private int _speechVoiceIndex = AppConfigDefaults.SpeechVoiceWindowsIndex;

    /// <summary>HP低下の対象プレイヤー(<see cref="Core.Models.PartyDisplayMode"/> の番号)。</summary>
    [ObservableProperty]
    private int _targetModeIndex;

    /// <summary>狙われるプレイヤーの対象プレイヤー(<see cref="AppConfigDefaults.TargetedPlayerAllIndex"/> など)。</summary>
    [ObservableProperty]
    private int _targetedPlayerModeIndex = AppConfigDefaults.DefaultTargetedPlayerModeIndex;

    /// <summary>狙われるプレイヤーの対象エンティティ(<see cref="AppConfigDefaults.TargetEntityBossOnlyIndex"/> など)。</summary>
    [ObservableProperty]
    private int _targetEntityModeIndex = AppConfigDefaults.DefaultTargetEntityModeIndex;

    /// <summary>狙われるプレイヤーの通知条件の番号(<see cref="AppConfigDefaults.TargetedDurationSeconds"/> の添字)。</summary>
    [ObservableProperty]
    private int _targetedConditionIndex = AppConfigDefaults.DefaultTargetedConditionIndex;

    /// <summary>HP低下とみなす割合の番号(<see cref="AppConfigDefaults.HealthLowThresholdPercents"/> の添字)。</summary>
    [ObservableProperty]
    private int _healthLowThresholdIndex = AppConfigDefaults.DefaultHealthLowThresholdIndex;

    /// <summary>HP低下の通知条件の番号(<see cref="AppConfigDefaults.HealthLowDurationSeconds"/> の添字)。</summary>
    [ObservableProperty]
    private int _healthLowConditionIndex = AppConfigDefaults.DefaultHealthLowConditionIndex;

    /// <summary>薬剤・料理効果切れの通知条件の番号(<see cref="AppConfigDefaults.CuisinePotionLowMinutes"/> の添字)。</summary>
    [ObservableProperty]
    private int _cuisinePotionConditionIndex = AppConfigDefaults.DefaultCuisinePotionConditionIndex;

    /// <summary>予兆技の通知条件の番号(<see cref="AppConfigDefaults.TelegraphedSkillLeadSeconds"/> の添字)。</summary>
    [ObservableProperty]
    private int _telegraphedSkillConditionIndex = AppConfigDefaults.DefaultTelegraphedSkillConditionIndex;

    /// <summary>
    /// エンティティ詠唱・プレイヤー出現・エンティティ出現の対象の名前と、ワールドチャットの一致ワード(区切りで並べた複数、<see cref="NotificationTargetList"/>)。
    /// 区切りは対象の名前が「|」、一致ワードが空白。打ったままの値で持ち、保存のときに分ける。
    /// </summary>
    [ObservableProperty]
    private string _targetName = string.Empty;

    /// <summary>
    /// エンティティ詠唱・プレイヤー出現・エンティティ出現の対象のID(区切りで並べた複数)。打ったままの値で持ち、保存のときに分けて数にする。空なら入力なし。
    /// 入力欄は数字と区切りだけを受け、区切りのあいだの桁は long に収まる長さまで(<see cref="Behaviors.DigitsOnlyInputBehavior"/>)。
    /// </summary>
    [ObservableProperty]
    private string _targetIdText = string.Empty;

    [ObservableProperty]
    private VoicevoxSpeakerOption? _selectedVoicevoxSpeaker;

    [ObservableProperty]
    private VoicevoxStyleOption? _selectedVoicevoxStyle;

    /// <summary>本体の項目。</summary>
    public NotificationItemViewModel(
        NotificationKind kind,
        string labelResourceKey,
        (string Key, string LabelResourceKey, string Placeholder)[] fieldDefinitions)
        : this(new HostNotificationTextSource(kind, fieldDefinitions), [])
    {
        Kind = kind;
        _labelResourceKey = labelResourceKey;
        RefreshLabel();
    }

    /// <summary>プラグインの項目。<paramref name="isFirstInGroup"/> ならプラグインの名前の見出しを出す。</summary>
    internal NotificationItemViewModel(PluginNotificationGroup group, PluginNotificationDefinition definition, bool isFirstInGroup)
        : this(new PluginNotificationTextSource(definition), [.. definition.Settings.Select(PluginSettingViewModel.Create)])
    {
        _pluginGroup = group;
        _pluginDefinition = definition;
        _isFirstInPluginGroup = isFirstInGroup;
        PluginItemKey = NotificationsConfig.GetPluginItemKey(group.PluginKey, definition.Id);
        RefreshLabel();
    }

    private NotificationItemViewModel(INotificationTextSource textSource, IReadOnlyList<PluginSettingViewModel> pluginSettings)
    {
        Text = new NotificationTextViewModel(textSource);
        Text.Changed += Text_Changed;
        PluginSettings = pluginSettings;
        foreach (var setting in pluginSettings)
        {
            setting.Changed += PluginSetting_Changed;
            setting.TextChanged += PluginSetting_TextChanged;
        }

        VoicevoxSpeakerOptions = new ReadOnlyObservableCollection<VoicevoxSpeakerOption>(_voicevoxSpeakerOptions);
        VoicevoxStyleOptions = new ReadOnlyObservableCollection<VoicevoxStyleOption>(_voicevoxStyleOptions);
    }

    /// <summary>スイッチ・出し方・音量・声・項目だけの設定が変わった。読み込みでは上げない。</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// 文章か対象の名前・IDが変わった(どれも打ち終わってから保存する)。読み込みと、言語を変えたときの既定の文の差し替えでは上げない。
    /// </summary>
    public event EventHandler? TextChanged;

    /// <summary>
    /// 読み上げの声を確かめる時機(読み上げの項目をオンにした・出し方を読み上げにした・読み上げ方法を変えた)。
    /// 画面が確かめてメッセージを出す。読み込みでは上げない。
    /// </summary>
    public event EventHandler? SpeechVoiceCheckRequested;

    /// <summary>本体の項目の種類。プラグインの項目は null。</summary>
    public NotificationKind? Kind { get; }

    /// <summary>プラグインの項目の保存の鍵(<see cref="NotificationsConfig.GetPluginItemKey"/>)。本体の項目は null。</summary>
    public string? PluginItemKey { get; }

    /// <summary>プラグインの項目の、どのプラグインの項目か。本体の項目は null。</summary>
    public string? PluginKey => _pluginGroup?.PluginKey;

    public bool HasGroupHeader => GroupHeader.Length > 0;

    /// <summary>プラグインが定義した設定の行(右下の、出し方の上)。本体の項目は空。</summary>
    public IReadOnlyList<PluginSettingViewModel> PluginSettings { get; }

    public NotificationTextViewModel Text { get; }

    /// <summary>開いているバフ・デバフカードとバフ・デバフグループのバフだけを通知する項目か。右下に注意書きを出す。</summary>
    public bool ShowsBuffWidgetNote => Kind == NotificationKind.BuffExpired;

    /// <summary>HP低下の項目か。右下に「対象プレイヤー」「HP低下判定」「通知条件」の行を出す。</summary>
    public bool ShowsHealthLowSettings => Kind == NotificationKind.HealthLow;

    /// <summary>狙われるプレイヤーの項目か。右下に「対象プレイヤー」(狙われるプレイヤーの選択肢)「対象エンティティ」「通知条件」の行を出す。</summary>
    public bool ShowsTargetedSettings => Kind == NotificationKind.Targeted;

    /// <summary>薬剤・料理効果切れの項目か。右下の先頭に「通知条件」の行を出す。</summary>
    public bool ShowsCuisinePotionCondition => Kind == NotificationKind.CuisinePotionLow;

    /// <summary>予兆技の項目か。右下の先頭に「通知条件」の行を出す。</summary>
    public bool ShowsTelegraphedSkillCondition => Kind == NotificationKind.TelegraphedSkill;

    /// <summary>エンティティ詠唱の項目か。右下の先頭に「対象スキル名」「対象スキルID」の行と注意書きを出す。</summary>
    public bool ShowsTargetSkill => Kind == NotificationKind.EntityCast;

    /// <summary>プレイヤー出現の項目か。右下の先頭に「対象プレイヤー名」「対象プレイヤーUID」の行と注意書きを出す。</summary>
    public bool ShowsTargetPlayerName => Kind == NotificationKind.PlayerAppeared;

    /// <summary>エンティティ出現の項目か。右下の先頭に「対象エンティティ名」「対象エンティティID」の行と注意書きを出す。</summary>
    public bool ShowsTargetEntityName => Kind == NotificationKind.EntityAppeared;

    /// <summary>ワールドチャットの項目か。右下の先頭に「一致ワード」の行と注意書きを出す。</summary>
    public bool ShowsMatchWords => Kind == NotificationKind.WorldChat;

    /// <summary>出し方が Windows の通知か。出し方の行に応答不可の注意書きを出す。</summary>
    public bool IsWindowsNotificationSelected => MethodIndex == AppConfigDefaults.NotificationMethodWindowsIndex;

    /// <summary>出し方が読み上げか。読み上げ方法の行を出す。</summary>
    public bool IsSpeechSelected => MethodIndex == AppConfigDefaults.NotificationMethodSpeechIndex;

    /// <summary>読み上げを VOICEVOX でするか。VOICEVOX の行(話者・スタイル)を出す。</summary>
    public bool IsVoicevoxSelected => IsSpeechSelected && SpeechVoiceIndex == AppConfigDefaults.SpeechVoiceVoicevoxIndex;

    public ReadOnlyObservableCollection<VoicevoxSpeakerOption> VoicevoxSpeakerOptions { get; }

    /// <summary>選んでいる話者のスタイル。</summary>
    public ReadOnlyObservableCollection<VoicevoxStyleOption> VoicevoxStyleOptions { get; }

    /// <summary>
    /// スタイルの行を出すか。エンジンの一覧で、選んでいる話者のスタイルが2つ以上あるときだけ出す。
    /// 一覧がまだ取れていないときはスタイルの数が分からないので、保存してあるスタイルを出す。
    /// </summary>
    public bool ShowsVoicevoxStyle => IsVoicevoxSelected && _isVoicevoxStyleRowNeeded;

    /// <summary>VOICEVOX の話者を選んであるか。選んでいなければ利用規約のリンクを出さない。</summary>
    public bool HasVoicevoxSpeaker => _voicevoxSpeakerUuid.Length > 0 && _voicevoxStyleId is not null;

    public string VoicevoxSpeakerUuid => _voicevoxSpeakerUuid;

    public string VoicevoxSpeakerName => _voicevoxSpeakerName;

    /// <summary>選んである話者の利用規約のリンクの文字。選んでいなければ空。</summary>
    public string VoicevoxPolicyLinkText => HasVoicevoxSpeaker
        ? LocalizationManager.Instance.Format("Voicevox_Policy_Title", _voicevoxSpeakerName)
        : string.Empty;

    public void Load(NotificationItemConfig config)
    {
        _isLoading = true;
        try
        {
            IsEnabled = config.IsEnabled;
            MethodIndex = config.MethodIndex;
            Volume = config.Volume;
            SpeechVoiceIndex = config.SpeechVoiceIndex;
            TargetModeIndex = config.TargetModeIndex;
            TargetedPlayerModeIndex = config.TargetedPlayerModeIndex;
            TargetEntityModeIndex = config.TargetEntityModeIndex;
            TargetedConditionIndex = config.TargetedConditionIndex;
            HealthLowThresholdIndex = config.HealthLowThresholdIndex;
            HealthLowConditionIndex = config.HealthLowConditionIndex;
            CuisinePotionConditionIndex = config.CuisinePotionConditionIndex;
            TelegraphedSkillConditionIndex = config.TelegraphedSkillConditionIndex;
            TargetName = ShowsMatchWords
                ? NotificationTargetList.JoinMatchWords(config.TargetNames)
                : NotificationTargetList.JoinNames(config.TargetNames);
            TargetIdText = NotificationTargetList.JoinIds(config.TargetIds);
            _voicevoxSpeakerUuid = config.VoicevoxSpeakerUuid;
            _voicevoxSpeakerName = config.VoicevoxSpeakerName;
            _voicevoxStyleId = config.VoicevoxStyleId;
            _voicevoxStyleName = config.VoicevoxStyleName;
            Text.Load(config.FormatString);
            RebuildVoicevoxOptions();
            OnVoicevoxSpeakerChanged();
        }
        finally
        {
            _isLoading = false;
        }
    }

    /// <summary>プラグインの項目の保存した値を読む。保存したことが無ければ null(全部既定)。</summary>
    public void LoadPlugin(PluginNotificationItemConfig? config)
    {
        Load(config?.Item ?? AppConfigDefaults.CreateNotificationItem());
        foreach (var setting in PluginSettings)
        {
            string? saved = null;
            config?.Settings.TryGetValue(setting.Definition.Id, out saved);
            setting.Load(saved);
        }
    }

    /// <summary>プラグインの項目の保存する値。</summary>
    public PluginNotificationItemConfig CreatePluginConfig()
    {
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in PluginSettings)
        {
            settings[setting.Definition.Id] = setting.CreateSavedValue();
        }

        return new PluginNotificationItemConfig
        {
            Item = CreateConfig(),
            Settings = settings
        };
    }

    public NotificationItemConfig CreateConfig()
    {
        return new NotificationItemConfig
        {
            IsEnabled = IsEnabled,
            FormatString = Text.CreateSavedValue(),
            MethodIndex = MethodIndex,
            Volume = (int)Math.Round(Volume, MidpointRounding.AwayFromZero),
            SpeechVoiceIndex = SpeechVoiceIndex,
            VoicevoxSpeakerUuid = _voicevoxSpeakerUuid,
            VoicevoxSpeakerName = _voicevoxSpeakerName,
            VoicevoxStyleId = _voicevoxStyleId,
            VoicevoxStyleName = _voicevoxStyleName,
            TargetModeIndex = TargetModeIndex,
            TargetedPlayerModeIndex = TargetedPlayerModeIndex,
            TargetEntityModeIndex = TargetEntityModeIndex,
            TargetedConditionIndex = TargetedConditionIndex,
            HealthLowThresholdIndex = HealthLowThresholdIndex,
            HealthLowConditionIndex = HealthLowConditionIndex,
            CuisinePotionConditionIndex = CuisinePotionConditionIndex,
            TelegraphedSkillConditionIndex = TelegraphedSkillConditionIndex,
            TargetNames = ShowsMatchWords
                ? NotificationTargetList.SplitMatchWords(TargetName)
                : NotificationTargetList.SplitNames(TargetName),
            TargetIds = NotificationTargetList.ParseIds(TargetIdText)
        };
    }

    /// <summary>
    /// エンジンから取れた話者の一覧を受けて、話者とスタイルの選択肢を作り直す。
    /// 読み上げ方法が VOICEVOX でまだ話者を選んでいなければ、一覧の先頭の話者(リストの一番上)とその最初のスタイル
    /// (話者を選び直したときと同じ決め方)を選んで保存する。選んである話者は、一覧に無くても替えない。
    /// </summary>
    public void ApplyFetchedVoicevoxStyles(IReadOnlyList<VoicevoxStyle> styles)
    {
        _fetchedVoicevoxStyles = styles;
        var selectsDefaultSpeaker = IsVoicevoxSelected && !HasVoicevoxSpeaker && styles.Count > 0;
        if (selectsDefaultSpeaker)
        {
            var defaultStyle = styles[0];
            _voicevoxSpeakerUuid = defaultStyle.SpeakerUuid;
            _voicevoxSpeakerName = defaultStyle.SpeakerName;
            _voicevoxStyleId = defaultStyle.StyleId;
            _voicevoxStyleName = defaultStyle.StyleName;
        }

        RebuildVoicevoxOptions();
        if (selectsDefaultSpeaker)
        {
            OnVoicevoxSpeakerChanged();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void RefreshLocalizedText()
    {
        RefreshLabel();
        Text.RefreshLocalizedText();
        foreach (var setting in PluginSettings)
        {
            setting.RefreshLocalizedText();
        }

        RebuildVoicevoxOptions();
        OnPropertyChanged(nameof(VoicevoxPolicyLinkText));
    }

    /// <summary>行の名前と見出し。本体の項目は文言の鍵、プラグインの項目は定義の名前とプラグインの名前。</summary>
    private void RefreshLabel()
    {
        var culture = LocalizationManager.Instance.CurrentCulture;
        if (_pluginDefinition is null)
        {
            Label = LocalizationManager.Instance.GetString(_labelResourceKey!);
            return;
        }

        Label = PluginTexts.Resolve(_pluginDefinition.Names, culture)
            ?? throw new InvalidOperationException($"The validated notification '{_pluginDefinition.Id}' has no name.");
        GroupHeader = _isFirstInPluginGroup ? _pluginGroup!.Info.GetDisplayName(culture) : string.Empty;
    }

    private void Text_Changed(object? sender, EventArgs e)
    {
        if (!_isLoading)
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void PluginSetting_Changed(object? sender, EventArgs e)
    {
        RaiseChanged();
    }

    private void PluginSetting_TextChanged(object? sender, EventArgs e)
    {
        if (!_isLoading)
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_isLoading)
        {
            return;
        }

        IsSelected = true;
        Changed?.Invoke(this, EventArgs.Empty);
        if (value && IsSpeechSelected)
        {
            SpeechVoiceCheckRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnMethodIndexChanged(int value)
    {
        OnSpeechSelectionChanged();
    }

    partial void OnSpeechVoiceIndexChanged(int value)
    {
        OnSpeechSelectionChanged();
    }

    partial void OnVolumeChanged(double value)
    {
        if (!_isLoading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnTargetModeIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnTargetedPlayerModeIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnTargetEntityModeIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnTargetedConditionIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnHealthLowThresholdIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnHealthLowConditionIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnCuisinePotionConditionIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnTelegraphedSkillConditionIndexChanged(int value)
    {
        RaiseChanged();
    }

    partial void OnTargetNameChanged(string value)
    {
        if (!_isLoading)
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnTargetIdTextChanged(string value)
    {
        if (!_isLoading)
        {
            TextChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RaiseChanged()
    {
        if (!_isLoading)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>出し方か読み上げ方法が変わった。読み上げで声を使うことになったら、声を確かめる。</summary>
    private void OnSpeechSelectionChanged()
    {
        OnPropertyChanged(nameof(IsWindowsNotificationSelected));
        OnPropertyChanged(nameof(IsSpeechSelected));
        OnPropertyChanged(nameof(IsVoicevoxSelected));
        OnPropertyChanged(nameof(ShowsVoicevoxStyle));

        if (_isLoading)
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        if (IsSpeechSelected)
        {
            SpeechVoiceCheckRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    partial void OnSelectedVoicevoxSpeakerChanged(VoicevoxSpeakerOption? value)
    {
        // 作り直しの途中と、選択リストが null を書き戻したときは、保存する値を変えない。
        if (_isRebuildingVoicevoxOptions || _isLoading || value is null || value.IsMissing)
        {
            return;
        }

        // 同じ話者が選び直されただけなら、選んでいるスタイルを保つ。
        if (string.Equals(value.SpeakerUuid, _voicevoxSpeakerUuid, StringComparison.Ordinal))
        {
            return;
        }

        // 話者を変えたら、その話者の一覧の最初のスタイルにする(VOICEVOX の既定のスタイルと同じ決め方)。
        // 保存してある話者のほかの選択肢は、エンジンの一覧から作ったものだけ。
        var defaultStyle = _fetchedVoicevoxStyles!.First(
            style => string.Equals(style.SpeakerUuid, value.SpeakerUuid, StringComparison.Ordinal));
        _voicevoxSpeakerUuid = value.SpeakerUuid;
        _voicevoxSpeakerName = value.SpeakerName;
        _voicevoxStyleId = defaultStyle.StyleId;
        _voicevoxStyleName = defaultStyle.StyleName;
        RebuildVoicevoxStyleOptions();
        OnVoicevoxSpeakerChanged();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    partial void OnSelectedVoicevoxStyleChanged(VoicevoxStyleOption? value)
    {
        if (_isRebuildingVoicevoxOptions || _isLoading || value is null || value.IsMissing)
        {
            return;
        }

        _voicevoxStyleId = value.StyleId;
        _voicevoxStyleName = value.StyleName;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnVoicevoxSpeakerChanged()
    {
        OnPropertyChanged(nameof(HasVoicevoxSpeaker));
        OnPropertyChanged(nameof(VoicevoxPolicyLinkText));
    }

    /// <summary>
    /// 話者とスタイルの選択肢を作り直す。選んである話者は押し直す。
    /// まだ問い合わせていないときは、選んである話者だけを選択肢にする。問い合わせた一覧に無ければ「見つかりません」付きで残す。
    /// </summary>
    private void RebuildVoicevoxOptions()
    {
        _isRebuildingVoicevoxOptions = true;
        try
        {
            _voicevoxSpeakerOptions.Clear();
            VoicevoxSpeakerOption? selected = null;
            var addedSpeakers = new HashSet<string>(StringComparer.Ordinal);

            foreach (var style in _fetchedVoicevoxStyles ?? [])
            {
                if (!addedSpeakers.Add(style.SpeakerUuid))
                {
                    continue;
                }

                var option = CreateVoicevoxSpeakerOption(style.SpeakerUuid, style.SpeakerName, isMissing: false);
                _voicevoxSpeakerOptions.Add(option);
                if (HasVoicevoxSpeaker && string.Equals(option.SpeakerUuid, _voicevoxSpeakerUuid, StringComparison.Ordinal))
                {
                    selected = option;
                }
            }

            if (selected is null && HasVoicevoxSpeaker)
            {
                selected = CreateVoicevoxSpeakerOption(
                    _voicevoxSpeakerUuid,
                    _voicevoxSpeakerName,
                    isMissing: _fetchedVoicevoxStyles is not null);
                _voicevoxSpeakerOptions.Insert(0, selected);
            }

            SelectedVoicevoxSpeaker = selected;
        }
        finally
        {
            _isRebuildingVoicevoxOptions = false;
        }

        OnPropertyChanged(nameof(SelectedVoicevoxSpeaker));
        RebuildVoicevoxStyleOptions();
    }

    /// <summary>
    /// 選んでいる話者のスタイルの選択肢を作り直し、スタイルの行を出すかを決める。選んであるスタイルは押し直す。
    /// 話者が一覧にあってスタイルだけ無ければ「見つかりません」付きで残す。話者ごと無いときは話者の行が知らせるので、スタイルの行は出さない。
    /// </summary>
    private void RebuildVoicevoxStyleOptions()
    {
        _isRebuildingVoicevoxOptions = true;
        try
        {
            _voicevoxStyleOptions.Clear();
            VoicevoxStyleOption? selected = null;
            var speakerFound = false;

            if (HasVoicevoxSpeaker)
            {
                foreach (var style in _fetchedVoicevoxStyles ?? [])
                {
                    if (!string.Equals(style.SpeakerUuid, _voicevoxSpeakerUuid, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    speakerFound = true;
                    var option = CreateVoicevoxStyleOption(style.StyleId, style.StyleName, isMissing: false);
                    _voicevoxStyleOptions.Add(option);
                    if (option.StyleId == _voicevoxStyleId)
                    {
                        selected = option;
                    }
                }

                if (selected is null)
                {
                    selected = CreateVoicevoxStyleOption(_voicevoxStyleId!.Value, _voicevoxStyleName, isMissing: speakerFound);
                    _voicevoxStyleOptions.Insert(0, selected);
                }
            }

            _isVoicevoxStyleRowNeeded = HasVoicevoxSpeaker
                && (_fetchedVoicevoxStyles is null || (speakerFound && _voicevoxStyleOptions.Count > 1));
            SelectedVoicevoxStyle = selected;
        }
        finally
        {
            _isRebuildingVoicevoxOptions = false;
        }

        OnPropertyChanged(nameof(SelectedVoicevoxStyle));
        OnPropertyChanged(nameof(ShowsVoicevoxStyle));
    }

    private static VoicevoxSpeakerOption CreateVoicevoxSpeakerOption(string speakerUuid, string speakerName, bool isMissing)
    {
        var displayName = isMissing
            ? LocalizationManager.Instance.Format("Settings_Notification_VoicevoxSpeakerMissing", speakerName)
            : speakerName;
        return new VoicevoxSpeakerOption(speakerUuid, speakerName, isMissing, displayName);
    }

    private static VoicevoxStyleOption CreateVoicevoxStyleOption(int styleId, string styleName, bool isMissing)
    {
        var displayName = isMissing
            ? LocalizationManager.Instance.Format("Settings_Notification_VoicevoxSpeakerMissing", styleName)
            : styleName;
        return new VoicevoxStyleOption(styleId, styleName, isMissing, displayName);
    }
}
