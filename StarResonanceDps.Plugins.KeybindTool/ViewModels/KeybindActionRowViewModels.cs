using System.Collections.ObjectModel;
using StarResonanceDps.Plugins.KeybindTool.Models;

namespace StarResonanceDps.Plugins.KeybindTool.ViewModels;

internal sealed class ControllerActionRowViewModel : ObservableObject
{
    private ControllerInputOption? _selectedButton;
    private ActionHelperOption? _selectedHelper;
    private string _displayName;
    private bool _isEditable = true;

    public ControllerActionRowViewModel(
        ControllerActionDefinition definition,
        string displayName)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _displayName = displayName ?? string.Empty;
    }

    public event EventHandler? SelectionChanged;

    public ControllerActionDefinition Definition { get; }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public void RefreshDisplayName(string displayName)
    {
        DisplayName = displayName ?? string.Empty;
    }

    public bool UsesHelper => Definition.UsesHelper;

    public ObservableCollection<ControllerInputOption> ButtonOptions { get; } = new();

    public ObservableCollection<ActionHelperOption> HelperOptions { get; } = new();

    public ControllerInputOption? SelectedButton
    {
        get => _selectedButton;
        set
        {
            if (SetProperty(ref _selectedButton, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public ActionHelperOption? SelectedHelper
    {
        get => _selectedHelper;
        set
        {
            if (SetProperty(ref _selectedHelper, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsEditable
    {
        get => _isEditable;
        set => SetProperty(ref _isEditable, value);
    }

    public bool IsLastInSection { get; set; }

    public void ReplaceButtonOptions(
        IEnumerable<ControllerInputOption> options,
        uint? preserveValue = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var selectedValue = preserveValue ?? SelectedButton?.Value;

        ButtonOptions.Clear();
        foreach (var option in options)
        {
            ButtonOptions.Add(option);
        }

        SelectedButton = selectedValue is null
            ? null
            : ButtonOptions.FirstOrDefault(option => option.Value == selectedValue.Value);
    }

    public void ReplaceHelperOptions(
        IEnumerable<ActionHelperOption> options,
        uint? preserveState = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var selectedState = preserveState ?? SelectedHelper?.StateValue;

        HelperOptions.Clear();
        foreach (var option in options)
        {
            HelperOptions.Add(option);
        }

        SelectedHelper = selectedState is null
            ? null
            : HelperOptions.FirstOrDefault(option => option.StateValue == selectedState.Value);
    }

    public void EnsureButtonOption(ControllerInputOption option)
    {
        if (ButtonOptions.All(existing => existing.Value != option.Value))
        {
            ButtonOptions.Add(option);
        }
    }

    public void SelectButtonValue(uint? value)
    {
        SelectedButton = value is null
            ? null
            : ButtonOptions.FirstOrDefault(option => option.Value == value.Value);
    }

    public void SelectHelperState(uint? stateValue)
    {
        SelectedHelper = stateValue is null
            ? null
            : HelperOptions.FirstOrDefault(option => option.StateValue == stateValue.Value);
    }
}

internal sealed class KeyMouseActionRowViewModel : ObservableObject
{
    private KeyMouseInputOption? _selectedKey;
    private string _displayName;
    private bool _isEditable = true;

    public KeyMouseActionRowViewModel(
        KeyMouseActionDefinition definition,
        string displayName,
        bool usesLControlPrefix)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _displayName = displayName ?? string.Empty;
        UsesLControlPrefix = usesLControlPrefix;
    }

    public event EventHandler? SelectionChanged;

    public KeyMouseActionDefinition Definition { get; }

    public string DisplayName
    {
        get => _displayName;
        private set => SetProperty(ref _displayName, value);
    }

    public void RefreshDisplayName(string displayName)
    {
        DisplayName = displayName ?? string.Empty;
    }

    public bool UsesLControlPrefix { get; }

    public string LControlPrefixDisplay => UsesLControlPrefix ? "L Ctrl +" : string.Empty;

    public ObservableCollection<KeyMouseInputOption> KeyOptions { get; } = new();

    public KeyMouseInputOption? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (SetProperty(ref _selectedKey, value))
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsEditable
    {
        get => _isEditable;
        set => SetProperty(ref _isEditable, value);
    }

    public bool IsLastInSection { get; set; }

    public void ReplaceKeyOptions(
        IEnumerable<KeyMouseInputOption> options,
        (uint InputType, uint Value)? preserveRecord = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var selectedRecord = preserveRecord
            ?? (SelectedKey is null
                ? ((uint InputType, uint Value)?)null
                : (SelectedKey.InputType, SelectedKey.Value));

        KeyOptions.Clear();
        foreach (var option in options)
        {
            KeyOptions.Add(option);
        }

        SelectedKey = selectedRecord is null
            ? null
            : KeyOptions.FirstOrDefault(option => option.InputType == selectedRecord.Value.InputType
                && option.Value == selectedRecord.Value.Value);
    }

    public void EnsureKeyOption(KeyMouseInputOption option)
    {
        if (KeyOptions.All(existing => existing.InputType != option.InputType || existing.Value != option.Value))
        {
            KeyOptions.Add(option);
        }
    }

    public void SelectKeyRecord(uint? inputType, uint? value)
    {
        SelectedKey = inputType is null || value is null
            ? null
            : KeyOptions.FirstOrDefault(option => option.InputType == inputType.Value
                && option.Value == value.Value);
    }
}
