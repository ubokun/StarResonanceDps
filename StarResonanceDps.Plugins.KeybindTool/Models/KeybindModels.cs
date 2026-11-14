using System.Text.Json.Serialization;

namespace StarResonanceDps.Plugins.KeybindTool.Models;

internal enum KeybindModeGroup
{
    Main,
    QuickWheel,
    Photo,
    Fishing
}

internal enum KeybindSaveTargetDevice
{
    Controller,
    KeyMouse
}

internal sealed record KeybindInvalidWriteOffset(
    int RelativeOffset,
    uint InputType,
    uint? StateValue);

internal sealed record KeybindInvalidWriteTarget(
    KeybindSaveTargetDevice Device,
    string ActionId,
    KeybindModeGroup Group,
    string ActionKey,
    IReadOnlyList<KeybindInvalidWriteOffset> InvalidOffsets);

internal sealed record KeybindWriteTargetValidation(
    IReadOnlyList<KeybindInvalidWriteTarget> InvalidTargets,
    IReadOnlySet<string> ControllerActionIdsToSkip,
    IReadOnlySet<string> KeyMouseActionIdsToSkip)
{
    public bool HasInvalidTargets => InvalidTargets.Count != 0;
}

internal interface IKeybindActionDefinition
{
    string Id { get; }

    KeybindModeGroup Group { get; }

    string Key { get; }
}

internal sealed record KeybindInputVisual(string? IconUri, string? Text)
{
    public bool HasIcon => !string.IsNullOrWhiteSpace(IconUri);

    public bool HasText => !string.IsNullOrWhiteSpace(Text);

    public static KeybindInputVisual TextOnly(string? text) => new(null, text);
}

internal sealed record ControllerInputOption(uint Value, string Label)
{
    public KeybindInputVisual Visual { get; init; } = KeybindInputVisual.TextOnly(Label);

    public override string ToString() => Label;
}

internal sealed record KeyMouseInputOption(uint InputType, uint Value, string Label)
{
    public KeybindInputVisual Visual { get; init; } = KeybindInputVisual.TextOnly(Label);

    public override string ToString() => Label;
}

internal sealed record HelperBindingOption(uint MainValue, string Label)
{
    public KeybindInputVisual Visual { get; init; } = KeybindInputVisual.TextOnly(Label);

    public override string ToString() => Label;
}

internal sealed record ActionHelperOption(uint StateValue, string Label)
{
    public KeybindInputVisual Visual { get; init; } = KeybindInputVisual.TextOnly(Label);

    public override string ToString() => Label;
}

internal sealed record PresetOption(uint Value, string Label)
{
    private KeybindInputVisual? _confirmVisual;
    private KeybindInputVisual? _cancelVisual;

    // Known presets provide two icon visuals. Unknown values retain their label as text.
    public KeybindInputVisual ConfirmVisual
    {
        get => _confirmVisual ?? KeybindInputVisual.TextOnly(Label);
        init => _confirmVisual = value;
    }

    public KeybindInputVisual CancelVisual
    {
        get => _cancelVisual ?? KeybindInputVisual.TextOnly(null);
        init => _cancelVisual = value;
    }

    public bool HasCancelVisual => CancelVisual.HasIcon || CancelVisual.HasText;

    public override string ToString() => Label;
}

internal sealed record ControllerActionDefinition(
    string Id,
    KeybindModeGroup Group,
    string Key,
    IReadOnlyList<int> RelativeOffsets,
    IReadOnlyList<uint> AllowedValues,
    bool HasExplicitAllowedValues,
    bool UsesHelper) : IKeybindActionDefinition;

internal sealed record KeyMouseActionDefinition(
    string Id,
    KeybindModeGroup Group,
    string Key,
    IReadOnlyList<int> RelativeOffsets,
    IReadOnlyList<uint> AllowedInputTypes) : IKeybindActionDefinition;

internal sealed class KeybindLayoutConfig
{
    [JsonPropertyName("version")]
    public int Version { get; set; } = 1;

    [JsonPropertyName("input_device")]
    public string? InputDevice { get; set; }

    [JsonPropertyName("controller_profile")]
    public ControllerLayoutProfile ControllerProfile { get; set; } = new();

    [JsonPropertyName("keymouse_profile")]
    public KeyMouseLayoutProfile KeyMouseProfile { get; set; } = new();
}

internal sealed class ControllerLayoutProfile
{
    [JsonPropertyName("controller_type")]
    public string? ControllerType { get; set; }

    [JsonPropertyName("quick_wheel_independent")]
    public bool QuickWheelIndependent { get; set; }

    [JsonPropertyName("photo_mode_independent")]
    public bool PhotoModeIndependent { get; set; }

    [JsonPropertyName("fishing_mode_independent")]
    public bool FishingModeIndependent { get; set; }

    [JsonPropertyName("keybind")]
    public ControllerKeybindProfile Keybind { get; set; } = new();

    [JsonPropertyName("actions")]
    public Dictionary<string, ControllerActionLayout> Actions { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class ControllerKeybindProfile
{
    [JsonPropertyName("helper1")]
    public string? Helper1 { get; set; }

    [JsonPropertyName("helper2")]
    public string? Helper2 { get; set; }

    [JsonPropertyName("preset")]
    public string? Preset { get; set; }
}

internal sealed class ControllerActionLayout
{
    [JsonPropertyName("helper")]
    public string? Helper { get; set; }

    [JsonPropertyName("button")]
    public string? Button { get; set; }
}

internal sealed class KeyMouseLayoutProfile
{
    [JsonPropertyName("keyboard_layout")]
    public string? KeyboardLayout { get; set; }

    [JsonPropertyName("quick_wheel_independent")]
    public bool QuickWheelIndependent { get; set; }

    [JsonPropertyName("photo_mode_independent")]
    public bool PhotoModeIndependent { get; set; }

    [JsonPropertyName("fishing_mode_independent")]
    public bool FishingModeIndependent { get; set; }

    [JsonPropertyName("actions")]
    public Dictionary<string, KeyMouseActionLayout> Actions { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class KeyMouseActionLayout
{
    [JsonPropertyName("key")]
    public string? Key { get; set; }
}

internal sealed record DetectedSaveFile(string DisplayName, string FilePath)
{
    public override string ToString() => DisplayName;
}
