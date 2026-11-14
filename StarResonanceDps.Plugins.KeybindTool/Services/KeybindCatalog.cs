using System.Collections.ObjectModel;
using System.Globalization;
using StarResonanceDps.Plugins.KeybindTool.Models;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.Plugins.KeybindTool.Services;

internal static class KeybindCatalog
{
    public const string DefaultControllerType = "PlayStation";
    public const string KeyboardLayoutJapanese = "Japanese";
    public const string KeyboardLayoutEnglish = "English";
    public const string DefaultKeyboardLayout = KeyboardLayoutEnglish;
    public const string InputDeviceController = "Controller";
    public const string InputDeviceKeyMouse = "KeyMouse";

    public const uint InputTypeKeyboard = 0x00000001u;
    public const uint InputTypeMouse = 0x00000002u;
    public const uint InputTypeController = 0x00000003u;

    public const uint ActionStateSingle = 0xFFFFFFFFu;
    public const uint ActionStateHelper1 = 0x00000000u;
    public const uint ActionStateHelper2 = 0x00000001u;

    public const string HelperNoneLabel = "——";
    public const string ButtonLayoutFileName = "keybind_config.json";

    public static readonly IReadOnlyList<string> ControllerTypes =
        new[] { "PlayStation", "Nintendo", "Xbox" };

    public static readonly IReadOnlyList<string> KeyboardLayouts =
        new[] { KeyboardLayoutEnglish, KeyboardLayoutJapanese };

    public static readonly IReadOnlyList<ControllerInputOption> ControllerInputOptions =
        new ControllerInputOption[]
        {
            new(0x1u, "LeftStickVertical"),
            new(0x2u, "LeftStickHorizontal"),
            new(0x3u, "RightStickVertical"),
            new(0x4u, "RightStickHorizontal"),
            new(0x5u, "LeftTrigger"),
            new(0x6u, "RightTrigger"),
            new(0x7u, "FaceSouth"),
            new(0x8u, "FaceEast"),
            new(0xAu, "FaceWest"),
            new(0xBu, "FaceNorth"),
            new(0xDu, "SystemLeft"),
            new(0xEu, "SystemRight"),
            new(0xFu, "SystemCenter"),
            new(0x11u, "LeftShoulder"),
            new(0x12u, "RightShoulder"),
            new(0x13u, "LeftStickPress"),
            new(0x14u, "RightStickPress"),
            new(0x17u, "DPadUp"),
            new(0x18u, "DPadDown"),
            new(0x19u, "DPadLeft"),
            new(0x1Au, "DPadRight")
        };

    public static readonly IReadOnlyList<KeyMouseInputOption> KeyMouseInputOptions =
        new KeyMouseInputOption[]
        {
            new(0x1u, 0x9u, "Tab"),
            new(0x1u, 0xDu, "Enter"),
            new(0x1u, 0x1Bu, "Esc"),
            new(0x1u, 0x20u, "Space"),
            new(0x1u, 0x27u, "'"),
            new(0x1u, 0x2Cu, ","),
            new(0x1u, 0x2Du, "-"),
            new(0x1u, 0x2Eu, "."),
            new(0x1u, 0x2Fu, "/"),
            new(0x1u, 0x30u, "0"),
            new(0x1u, 0x31u, "1"),
            new(0x1u, 0x32u, "2"),
            new(0x1u, 0x33u, "3"),
            new(0x1u, 0x34u, "4"),
            new(0x1u, 0x35u, "5"),
            new(0x1u, 0x36u, "6"),
            new(0x1u, 0x37u, "7"),
            new(0x1u, 0x38u, "8"),
            new(0x1u, 0x39u, "9"),
            new(0x1u, 0x3Bu, ";"),
            new(0x1u, 0x3Du, "="),
            new(0x1u, 0x5Bu, "["),
            new(0x1u, 0x5Cu, "\\"),
            new(0x1u, 0x5Du, "]"),
            new(0x1u, 0x60u, "`"),
            new(0x1u, 0x61u, "A"),
            new(0x1u, 0x62u, "B"),
            new(0x1u, 0x63u, "C"),
            new(0x1u, 0x64u, "D"),
            new(0x1u, 0x65u, "E"),
            new(0x1u, 0x66u, "F"),
            new(0x1u, 0x67u, "G"),
            new(0x1u, 0x68u, "H"),
            new(0x1u, 0x69u, "I"),
            new(0x1u, 0x6Au, "J"),
            new(0x1u, 0x6Bu, "K"),
            new(0x1u, 0x6Cu, "L"),
            new(0x1u, 0x6Du, "M"),
            new(0x1u, 0x6Eu, "N"),
            new(0x1u, 0x6Fu, "O"),
            new(0x1u, 0x70u, "P"),
            new(0x1u, 0x71u, "Q"),
            new(0x1u, 0x72u, "R"),
            new(0x1u, 0x73u, "S"),
            new(0x1u, 0x74u, "T"),
            new(0x1u, 0x75u, "U"),
            new(0x1u, 0x76u, "V"),
            new(0x1u, 0x77u, "W"),
            new(0x1u, 0x78u, "X"),
            new(0x1u, 0x79u, "Y"),
            new(0x1u, 0x7Au, "Z"),
            new(0x1u, 0x100u, "Num0"),
            new(0x1u, 0x101u, "Num1"),
            new(0x1u, 0x102u, "Num2"),
            new(0x1u, 0x103u, "Num3"),
            new(0x1u, 0x104u, "Num4"),
            new(0x1u, 0x105u, "Num5"),
            new(0x1u, 0x106u, "Num6"),
            new(0x1u, 0x107u, "Num7"),
            new(0x1u, 0x108u, "Num8"),
            new(0x1u, 0x109u, "Num9"),
            new(0x1u, 0x111u, "↑"),
            new(0x1u, 0x112u, "↓"),
            new(0x1u, 0x113u, "→"),
            new(0x1u, 0x114u, "←"),
            new(0x1u, 0x11Au, "F1"),
            new(0x1u, 0x11Bu, "F2"),
            new(0x1u, 0x11Cu, "F3"),
            new(0x1u, 0x11Du, "F4"),
            new(0x1u, 0x11Eu, "F5"),
            new(0x1u, 0x11Fu, "F6"),
            new(0x1u, 0x120u, "F7"),
            new(0x1u, 0x121u, "F8"),
            new(0x1u, 0x122u, "F9"),
            new(0x1u, 0x123u, "F10"),
            new(0x1u, 0x124u, "F11"),
            new(0x1u, 0x125u, "F12"),
            new(0x1u, 0x12Fu, "R Shift"),
            new(0x1u, 0x130u, "L Shift"),
            new(0x1u, 0x131u, "R Ctrl"),
            new(0x1u, 0x132u, "L Ctrl"),
            new(0x1u, 0x133u, "R Alt"),
            new(0x1u, 0x134u, "L Alt"),
            new(0x2u, 0x0u, "LeftClick"),
            new(0x2u, 0x1u, "RightClick"),
            new(0x2u, 0x2u, "MiddleButton"),
            new(0x2u, 0x3u, "M3"),
            new(0x2u, 0x4u, "M4"),
            new(0x2u, 0x5u, "M5"),
            new(0x2u, 0x6u, "M6"),
            new(0x2u, 0x7u, "ScrollWheel")
        };

    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerMainActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.Main, "MoveForwardBack", new[] { 0xB2 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "MoveLeftRight", new[] { 0xC7 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "CameraForwardBack", new[] { 0x60F }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "CameraLeftRight", new[] { 0x624 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "Jump", new[] { 0x133 }),
            CreateControllerAction(KeybindModeGroup.Main, "DashDodge", new[] { 0x1C7 }),
            CreateControllerAction(KeybindModeGroup.Main, "EnvironmentalResonance1", new[] { 0x204 }),
            CreateControllerAction(KeybindModeGroup.Main, "EnvironmentalResonance2", new[] { 0x241 }),
            CreateControllerAction(KeybindModeGroup.Main, "NormalAttack", new[] { 0x27E }),
            CreateControllerAction(KeybindModeGroup.Main, "SpecialAttack", new[] { 0x9E7 }),
            CreateControllerAction(KeybindModeGroup.Main, "MasterySkill1", new[] { 0x2D5 }),
            CreateControllerAction(KeybindModeGroup.Main, "MasterySkill2", new[] { 0x312 }),
            CreateControllerAction(KeybindModeGroup.Main, "MasterySkill3", new[] { 0x34F }),
            CreateControllerAction(KeybindModeGroup.Main, "MasterySkill4", new[] { 0x38C }),
            CreateControllerAction(KeybindModeGroup.Main, "UltimateSkill", new[] { 0x9AA }),
            CreateControllerAction(KeybindModeGroup.Main, "BattleImagine1", new[] { 0xA24 }),
            CreateControllerAction(KeybindModeGroup.Main, "BattleImagine2", new[] { 0xA61 }),
            CreateControllerAction(KeybindModeGroup.Main, "CycleItemsLeft", new[] { 0x102D }),
            CreateControllerAction(KeybindModeGroup.Main, "UseItem", new[] { 0x3C9 }),
            CreateControllerAction(KeybindModeGroup.Main, "CycleItemsRight", new[] { 0x106A }),
            CreateControllerAction(KeybindModeGroup.Main, "Action", new[] { 0x551 }),
            CreateControllerAction(KeybindModeGroup.Main, "LockOnSwitch", new[] { 0x406 }),
            CreateControllerAction(KeybindModeGroup.Main, "ExtraSkill", new[] { 0xA9E }),
            CreateControllerAction(KeybindModeGroup.Main, "CancelInteraction", new[] { 0x45D }),
            CreateControllerAction(KeybindModeGroup.Main, "TrackQuest", new[] { 0x514 }),
            CreateControllerAction(KeybindModeGroup.Main, "HideUi", new[] { 0x4D7 }),
            CreateControllerAction(KeybindModeGroup.Main, "QuickUseQuestItem", new[] { 0x49A }),
            CreateControllerAction(KeybindModeGroup.Main, "MapOnOff", new[] { 0x690 }),
            CreateControllerAction(KeybindModeGroup.Main, "Quests", new[] { 0x6CD }),
            CreateControllerAction(KeybindModeGroup.Main, "SocialMode", new[] { 0x70A }),
            CreateControllerAction(KeybindModeGroup.Main, "OpenMenu", new[] { 0x84D }),
            CreateControllerAction(KeybindModeGroup.Main, "CloseMenu", new[] { 0x17CE }),
            CreateControllerAction(KeybindModeGroup.Main, "CursorMoveUpDown", new[] { 0x1F40 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "CursorMoveLeftRight", new[] { 0x1F63 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Main, "TakePhoto", new[] { 0x76A }),
            CreateControllerAction(KeybindModeGroup.Main, "LeaveDungeon", new[] { 0x810 }),
            CreateControllerAction(KeybindModeGroup.Main, "UseItemAlternate", new[] { 0x90D }),
            CreateControllerAction(KeybindModeGroup.Main, "QuickAction", new[] { 0xBA4 }),
            CreateControllerAction(KeybindModeGroup.Main, "SummonDismissMount", new[] { 0xB44 }),
            CreateControllerAction(KeybindModeGroup.Main, "AcceptInvite", new[] { 0xBE1 }),
            CreateControllerAction(KeybindModeGroup.Main, "DeclineInvite", new[] { 0xC1E }),
            CreateControllerAction(KeybindModeGroup.Main, "AutoBattle", new[] { 0xCBB }),
            CreateControllerAction(KeybindModeGroup.Main, "Channel", new[] { 0xC7E }),
            CreateControllerAction(KeybindModeGroup.Main, "IllustrationGuide", new[] { 0xCF8 }),
            CreateControllerAction(KeybindModeGroup.Main, "QuickWheel", new[] { 0xD35 }),
            CreateControllerAction(KeybindModeGroup.Main, "SwitchQuestLeft", new[] { 0xFB3 }),
            CreateControllerAction(KeybindModeGroup.Main, "SwitchQuestRight", new[] { 0xFF0 }),
            CreateControllerAction(KeybindModeGroup.Main, "ZoomOut", new[] { 0x58E }),
            CreateControllerAction(KeybindModeGroup.Main, "ZoomIn", new[] { 0x5A3 }),
            CreateControllerAction(KeybindModeGroup.Main, "OpenSkillPalette", new[] { 0x1227 }),
            CreateControllerAction(KeybindModeGroup.Main, "RoleSkill1", new[] { 0x1133 }),
            CreateControllerAction(KeybindModeGroup.Main, "RoleSkill2", new[] { 0x1170 }),
            CreateControllerAction(KeybindModeGroup.Main, "RoleSkill3", new[] { 0x11AD }),
            CreateControllerAction(KeybindModeGroup.Main, "RoleSkill4", new[] { 0x11EA }),
            CreateControllerAction(KeybindModeGroup.Main, "HomeBlueprint", new[] { 0x1264 }),
        };
    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerQuickWheelActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.QuickWheel, "SwitchQuickWheelLeft", new[] { 0x289C }, usesHelper: false),
            CreateControllerAction(KeybindModeGroup.QuickWheel, "SwitchQuickWheelRight", new[] { 0x28B1 }, usesHelper: false),
            CreateControllerAction(KeybindModeGroup.QuickWheel, "EditQuickWheel", new[] { 0x28EE }, usesHelper: false),
            CreateControllerAction(KeybindModeGroup.QuickWheel, "CloseQuickWheel", new[] { 0x292B }),
        };
    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerPhotoActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.Photo, "CameraMoveUpDown", new[] { 0x230E }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "CameraMoveLeftRight", new[] { 0x2323 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "CameraPanForwardBack", new[] { 0x239F }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "CameraPanLeftRight", new[] { 0x23B4 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "HideInterface", new[] { 0x21B8 }),
            CreateControllerAction(KeybindModeGroup.Photo, "TakePhoto", new[] { 0x213E }),
            CreateControllerAction(KeybindModeGroup.Photo, "SettingsMenu", new[] { 0x2688 }),
            CreateControllerAction(KeybindModeGroup.Photo, "ParticipantMenu", new[] { 0x26C5 }),
            CreateControllerAction(KeybindModeGroup.Photo, "ShowCursor", new[] { 0x27B8 }),
            CreateControllerAction(KeybindModeGroup.Photo, "CloseMenu", new[] { 0x226F }),
            CreateControllerAction(KeybindModeGroup.Photo, "MoveForwardBack", new[] { 0x2430 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "MoveLeftRight", new[] { 0x2445 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "CameraForwardBack", new[] { 0x273A }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "CameraLeftRight", new[] { 0x274F }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "Jump", new[] { 0x205D }),
            CreateControllerAction(KeybindModeGroup.Photo, "DashDodge", new[] { 0x217B }),
            CreateControllerAction(KeybindModeGroup.Photo, "SpecialAttack", new[] { 0x24B1 }),
            CreateControllerAction(KeybindModeGroup.Photo, "MasterySkill1", new[] { 0x24EE }),
            CreateControllerAction(KeybindModeGroup.Photo, "MasterySkill2", new[] { 0x252B }),
            CreateControllerAction(KeybindModeGroup.Photo, "MasterySkill3", new[] { 0x2568 }),
            CreateControllerAction(KeybindModeGroup.Photo, "MasterySkill4", new[] { 0x25A5 }),
            CreateControllerAction(KeybindModeGroup.Photo, "UltimateSkill", new[] { 0x209A }),
            CreateControllerAction(KeybindModeGroup.Photo, "SummonDismissMount", new[] { 0x25E2 }),
            CreateControllerAction(KeybindModeGroup.Photo, "CursorMoveUpDown", new[] { 0x27FE }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "CursorMoveLeftRight", new[] { 0x2821 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Photo, "ZoomOut", new[] { 0x20EC }),
            CreateControllerAction(KeybindModeGroup.Photo, "ZoomIn", new[] { 0x2101 }),
        };
    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerFishingActions =
        new ControllerActionDefinition[]
        {
            CreateControllerAction(KeybindModeGroup.Fishing, "RodMoveForwardBack", new[] { 0x2BB0 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Fishing, "RodMoveLeftRight", new[] { 0x2BC5 }, allowedValues: new[] { 0x1u, 0x2u, 0x3u, 0x4u }),
            CreateControllerAction(KeybindModeGroup.Fishing, "CastReelIn", new[] { 0x29A3 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "FishingEncyclopedia", new[] { 0x29E0 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "FishingResearch", new[] { 0x2A1D }),
            CreateControllerAction(KeybindModeGroup.Fishing, "SwitchBait", new[] { 0x2A5A }),
            CreateControllerAction(KeybindModeGroup.Fishing, "SwitchRod", new[] { 0x2A97 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "ModeGuide", new[] { 0x2AD4 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "Settings", new[] { 0x2B11 }),
            CreateControllerAction(KeybindModeGroup.Fishing, "CloseMenu", new[] { 0x2C3F }),
            CreateControllerAction(KeybindModeGroup.Fishing, "SocialMode", new[] { 0x2C02 }),
        };

    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseMainActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.Main, "MoveForward", new[] { 0x59, 0x12BD }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MoveBack", new[] { 0x6E, 0x12D2 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MoveLeft", new[] { 0x83, 0x12E7 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MoveRight", new[] { 0x98, 0x12FC }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ToggleWalkRun", new[] { 0x156, 0x1371 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Jump", new[] { 0x119, 0x134E }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "DashDodge1", new[] { 0x193, 0x13AE }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "DashDodge2", new[] { 0x1AD }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "EnvironmentalResonance1", new[] { 0x1EA, 0x13D1 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "EnvironmentalResonance2", new[] { 0x227 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "NormalAttack", new[] { 0x264 }, allowedInputTypes: new[] { 0x2u }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "SpecialAttack", new[] { 0x9CD, 0x1934 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MasterySkill1", new[] { 0x2BB, 0x143A }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MasterySkill2", new[] { 0x2F8, 0x145D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MasterySkill3", new[] { 0x335, 0x1480 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MasterySkill4", new[] { 0x372, 0x14A3 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "UltimateSkill", new[] { 0x990, 0x1911 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "BattleImagine1", new[] { 0xA0A, 0x1957 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "BattleImagine2", new[] { 0xA47, 0x197A }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "CycleItemsLeft", new[] { 0x1013, 0x1E28 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "UseItem", new[] { 0x3AF, 0x14C6 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "CycleItemsRight", new[] { 0x1050, 0x1E4B }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Action", new[] { 0x537, 0x1598 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "LockOnSwitch1", new[] { 0x3EC, 0x14E9 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "LockOnSwitch2", new[] { 0x420 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ExtraSkill", new[] { 0xA84, 0x199D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "CancelInteraction", new[] { 0x443, 0x150C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "TrackQuest", new[] { 0x4FA, 0x1575 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "HideUi", new[] { 0x4BD, 0x1552 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickUseQuestItem", new[] { 0x480, 0x152F }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "RecommendedEvents", new[] { 0xB07, 0x1A06 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "MapOnOff", new[] { 0x676, 0x1679 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Quests", new[] { 0x6B3, 0x169C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "SocialMode", new[] { 0x6F0, 0x16BF }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Talk", new[] { 0xC41, 0x1B0C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Character", new[] { 0x72D, 0x16E2 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Guild", new[] { 0xE70, 0x1CD3 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ChatChannelUp", new[] { 0x108D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ChatChannelDown", new[] { 0x10B0 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ChatInputChannelLeft", new[] { 0x10D3 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ChatInputChannelRight", new[] { 0x10F6 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "OpenMenu", new[] { 0x833 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "CloseMenu", new[] { 0x17B4 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ShowMouseCursor", new[] { 0x870, 0x17F1 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "TakePhoto", new[] { 0x750, 0x1705 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Inventory", new[] { 0x78D, 0x1728 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Party", new[] { 0x7B0, 0x174B }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "SeasonCenter", new[] { 0x7D3, 0x176E }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "LeaveDungeon", new[] { 0x7F6, 0x1791 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Abilities", new[] { 0x8D0, 0x1851 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "UseItemAlternate", new[] { 0x8F3, 0x1874 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickAction", new[] { 0xB8A, 0x1A6F }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "SummonDismissMount", new[] { 0xB2A, 0x1A29 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "TogglePartyVoice", new[] { 0xB67, 0x1A4C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "AcceptInvite", new[] { 0xBC7, 0x1A92 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "DeclineInvite", new[] { 0xC04, 0x1ACF }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "AutoBattle", new[] { 0xCA1, 0x1B52 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Channel", new[] { 0xC64, 0x1B2F }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "IllustrationGuide", new[] { 0xCDE, 0x1B75 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheel", new[] { 0xD1B, 0x1B98 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "AutoRun", new[] { 0xF39, 0x1D9C }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "SwitchQuestLeft", new[] { 0xF99, 0x1DE2 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "SwitchQuestRight", new[] { 0xFD6 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "EditHome", new[] { 0xF16, 0x1D79 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "ZoomOutIn", new[] { 0x574, 0x893, 0x15D5, 0x1814 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "OpenSkillPalette", new[] { 0x120D, 0x1F86 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "RoleSkill1", new[] { 0x1119 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "RoleSkill2", new[] { 0x1156 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "RoleSkill3", new[] { 0x1193 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "RoleSkill4", new[] { 0x11D0 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "HomeBlueprint", new[] { 0x124A }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "SummonAttendantImagine", new[] { 0x1287, 0x2000 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot1", new[] { 0xD58, 0x1BBB }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot2", new[] { 0xD7B, 0x1BDE }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot3", new[] { 0xD9E, 0x1C01 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot4", new[] { 0xDC1, 0x1C24 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot5", new[] { 0xDE4, 0x1C47 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot6", new[] { 0xE07, 0x1C6A }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot7", new[] { 0xE2A, 0x1C8D }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "QuickWheelSlot8", new[] { 0xE4D, 0x1CB0 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Skills", new[] { 0xE93, 0x1CF6 }),
            CreateKeyMouseAction(KeybindModeGroup.Main, "Equipment", new[] { 0xEB6, 0x1D19 }),
        };
    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseQuickWheelActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.QuickWheel, "SwitchQuickWheel", new[] { 0x2882 }),
            CreateKeyMouseAction(KeybindModeGroup.QuickWheel, "EditQuickWheel", new[] { 0x28D4 }),
            CreateKeyMouseAction(KeybindModeGroup.QuickWheel, "CloseQuickWheel", new[] { 0x2911 }),
        };
    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMousePhotoActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraMoveUp", new[] { 0x22B5 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraMoveDown", new[] { 0x22CA }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraMoveLeft", new[] { 0x22DF }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraMoveRight", new[] { 0x22F4 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraPanForward", new[] { 0x2346 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraPanBack", new[] { 0x235B }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraPanLeft", new[] { 0x2370 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CameraPanRight", new[] { 0x2385 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ZoomOut", new[] { 0x20BD }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ZoomIn", new[] { 0x20D2 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "HideInterface", new[] { 0x219E }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "TakePhoto", new[] { 0x2124 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "SettingsMenu", new[] { 0x266E }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ParticipantMenu", new[] { 0x26AB }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MoveForward", new[] { 0x23D7 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MoveBack", new[] { 0x23EC }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MoveLeft", new[] { 0x2401 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MoveRight", new[] { 0x2416 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "Jump", new[] { 0x2043 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "DashDodge", new[] { 0x2161 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "ToggleWalkRun", new[] { 0x26E8 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "SpecialAttack", new[] { 0x2497 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MasterySkill1", new[] { 0x24D4 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MasterySkill2", new[] { 0x2511 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MasterySkill3", new[] { 0x254E }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "MasterySkill4", new[] { 0x258B }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "UltimateSkill", new[] { 0x2080 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "SummonDismissMount", new[] { 0x25C8 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "Exit", new[] { 0x2605 }),
            CreateKeyMouseAction(KeybindModeGroup.Photo, "CloseMenu", new[] { 0x2255 }),
        };
    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseFishingActions =
        new KeyMouseActionDefinition[]
        {
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "CastReelIn", new[] { 0x2989 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "RodMoveForward", new[] { 0x2B57 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "RodMoveBack", new[] { 0x2B6C }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "RodMoveLeft", new[] { 0x2B81 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "RodMoveRight", new[] { 0x2B96 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "FishingEncyclopedia", new[] { 0x29C6 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "FishingResearch", new[] { 0x2A03 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "SwitchBait", new[] { 0x2A40 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "SwitchRod", new[] { 0x2A7D }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "ModeGuide", new[] { 0x2ABA }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "Settings", new[] { 0x2AF7 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "ShowMouseCursor", new[] { 0x2B34 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "CloseMenu", new[] { 0x2C25 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "SocialMode", new[] { 0x2BE8 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "Talk", new[] { 0x2C62 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "ChatChannelUp", new[] { 0x2C85 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "ChatChannelDown", new[] { 0x2CA8 }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "ChatInputChannelLeft", new[] { 0x2CCB }),
            CreateKeyMouseAction(KeybindModeGroup.Fishing, "ChatInputChannelRight", new[] { 0x2CEE }),
        };

    public static readonly IReadOnlyList<ControllerActionDefinition> ControllerActions =
        ControllerMainActions
            .Concat(ControllerQuickWheelActions)
            .Concat(ControllerPhotoActions)
            .Concat(ControllerFishingActions)
            .ToArray();

    public static readonly IReadOnlyList<KeyMouseActionDefinition> KeyMouseActions =
        KeyMouseMainActions
            .Concat(KeyMouseQuickWheelActions)
            .Concat(KeyMousePhotoActions)
            .Concat(KeyMouseFishingActions)
            .ToArray();

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> KeyboardInputLabelOverrides =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>>(
            new Dictionary<string, IReadOnlyDictionary<uint, string>>(StringComparer.OrdinalIgnoreCase)
            {
                [KeyboardLayoutJapanese] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>
                {
                    [0x27u] = ":",
                    [0x2Cu] = "<",
                    [0x2Eu] = ">",
                    [0x3Du] = "^",
                    [0x5Bu] = "@",
                    [0x5Cu] = "]",
                    [0x5Du] = "[",
                    [0x60u] = "~"
                })
            });

    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>> ControllerInputLabels =
        new ReadOnlyDictionary<string, IReadOnlyDictionary<uint, string>>(
            new Dictionary<string, IReadOnlyDictionary<uint, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["PlayStation"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>
                {
                    [1u] = "LeftStickVertical",
                    [2u] = "LeftStickHorizontal",
                    [3u] = "RightStickVertical",
                    [4u] = "RightStickHorizontal",
                    [5u] = "L2",
                    [6u] = "R2",
                    [7u] = "Cross",
                    [8u] = "Circle",
                    [10u] = "Square",
                    [11u] = "Triangle",
                    [13u] = "TouchPad",
                    [14u] = "Options",
                    [15u] = "Share",
                    [17u] = "L1",
                    [18u] = "R1",
                    [19u] = "L3",
                    [20u] = "R3",
                    [23u] = "DPadUp",
                    [24u] = "DPadDown",
                    [25u] = "DPadLeft",
                    [26u] = "DPadRight"
                }),
                ["Nintendo"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>
                {
                    [1u] = "LeftStickVertical",
                    [2u] = "LeftStickHorizontal",
                    [3u] = "RightStickVertical",
                    [4u] = "RightStickHorizontal",
                    [5u] = "ZL",
                    [6u] = "ZR",
                    [7u] = "B",
                    [8u] = "A",
                    [10u] = "Y",
                    [11u] = "X",
                    [13u] = "Minus",
                    [14u] = "Plus",
                    [15u] = "Capture",
                    [17u] = "L",
                    [18u] = "R",
                    [19u] = "LeftStick",
                    [20u] = "RightStick",
                    [23u] = "DPadUp",
                    [24u] = "DPadDown",
                    [25u] = "DPadLeft",
                    [26u] = "DPadRight"
                }),
                ["Xbox"] = new ReadOnlyDictionary<uint, string>(new Dictionary<uint, string>
                {
                    [1u] = "LeftStickVertical",
                    [2u] = "LeftStickHorizontal",
                    [3u] = "RightStickVertical",
                    [4u] = "RightStickHorizontal",
                    [5u] = "LT",
                    [6u] = "RT",
                    [7u] = "A",
                    [8u] = "B",
                    [10u] = "X",
                    [11u] = "Y",
                    [13u] = "View",
                    [14u] = "Menu",
                    [15u] = "Xbox",
                    [17u] = "LB",
                    [18u] = "RB",
                    [19u] = "LeftStick",
                    [20u] = "RightStick",
                    [23u] = "DPadUp",
                    [24u] = "DPadDown",
                    [25u] = "DPadLeft",
                    [26u] = "DPadRight"
                })
            });

    public static readonly IReadOnlyDictionary<uint, uint> HelperMainToActionValue =
        new ReadOnlyDictionary<uint, uint>(
            new Dictionary<uint, uint>
            {
                [0x01u] = 17u,
                [0x02u] = 18u,
                [0x04u] = 5u,
                [0x08u] = 6u
            });

    private static readonly HashSet<string> KeyMouseLControlPrefixActionIds = new(StringComparer.Ordinal)
    {
        GetActionId(KeybindModeGroup.Main, "HideUi"),
        GetActionId(KeybindModeGroup.Main, "TogglePartyVoice"),
        GetActionId(KeybindModeGroup.Main, "RoleSkill1"),
        GetActionId(KeybindModeGroup.Main, "RoleSkill2"),
        GetActionId(KeybindModeGroup.Main, "RoleSkill3"),
        GetActionId(KeybindModeGroup.Main, "RoleSkill4")
    };

    public static readonly IReadOnlyDictionary<string, string> ControllerQuickWheelLinks =
        CreateDirectLink(KeybindModeGroup.QuickWheel, "CloseQuickWheel", KeybindModeGroup.Main, "QuickWheel");

    public static readonly IReadOnlyDictionary<string, string> KeyMouseQuickWheelLinks =
        CreateDirectLink(KeybindModeGroup.QuickWheel, "CloseQuickWheel", KeybindModeGroup.Main, "QuickWheel");

    public static readonly IReadOnlyDictionary<string, string> ControllerPhotoModeLinks =
        BuildModeLinks(
            ControllerMainActions,
            ControllerPhotoActions,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "TakePhoto", "CloseMenu" });

    public static readonly IReadOnlyDictionary<string, string> ControllerFishingModeLinks =
        BuildModeLinks(
            ControllerMainActions,
            ControllerFishingActions,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal) { "CloseMenu" });

    public static readonly IReadOnlyDictionary<string, string> KeyMousePhotoModeLinks =
        BuildModeLinks(
            KeyMouseMainActions,
            KeyMousePhotoActions,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["CameraForward"] = "CameraPanForward",
                ["CameraBack"] = "CameraPanBack",
                ["CameraLeft"] = "CameraPanLeft",
                ["CameraRight"] = "CameraPanRight",
                ["DashDodge1"] = "DashDodge",
                ["TakePhoto"] = "Exit"
            },
            new HashSet<string>(StringComparer.Ordinal) { "TakePhoto" });

    public static readonly IReadOnlyDictionary<string, string> KeyMouseFishingModeLinks =
        BuildModeLinks(
            KeyMouseMainActions,
            KeyMouseFishingActions,
            new Dictionary<string, string>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

    static KeybindCatalog()
    {
        ValidateSlotOwnership();
    }

    private static void ValidateSlotOwnership()
    {
        var writeRanges = new List<(int Start, int End, string Device, string ActionId, int RelativeOffset)>();

        foreach (var action in ControllerActions)
        {
            foreach (var relativeOffset in action.RelativeOffsets)
            {
                writeRanges.Add((
                    relativeOffset - sizeof(uint),
                    relativeOffset + (sizeof(uint) * 2),
                    "controller",
                    action.Id,
                    relativeOffset));
            }
        }

        foreach (var action in KeyMouseActions)
        {
            foreach (var relativeOffset in action.RelativeOffsets)
            {
                writeRanges.Add((
                    relativeOffset - sizeof(uint),
                    relativeOffset + sizeof(uint),
                    "keymouse",
                    action.Id,
                    relativeOffset));
            }
        }

        for (var currentIndex = 0; currentIndex < writeRanges.Count; currentIndex++)
        {
            var current = writeRanges[currentIndex];
            for (var otherIndex = currentIndex + 1; otherIndex < writeRanges.Count; otherIndex++)
            {
                var other = writeRanges[otherIndex];
                if (Math.Max(current.Start, other.Start) >= Math.Min(current.End, other.End))
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "Keybind binding write ranges overlap: "
                    + $"{current.Device}:{current.ActionId}@0x{current.RelativeOffset:X5} / "
                    + $"{other.Device}:{other.ActionId}@0x{other.RelativeOffset:X5}");
            }
        }
    }

    public static string GetActionId(KeybindModeGroup group, string key)
    {
        var prefix = GetActionGroupPrefix(group);
        return $"{prefix}:{key}";
    }

    public static string GetActionLocalizationKey(IKeybindActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return GetActionLocalizationKey(definition.Group, definition.Key);
    }

    public static string GetActionLocalizationKey(KeybindModeGroup group, string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return $"Keybind.Action.{group}.{key}";
    }

    public static string GetControllerInputStorageKey(uint value)
    {
        return value switch
        {
            1u => "LeftStickVertical",
            2u => "LeftStickHorizontal",
            3u => "RightStickVertical",
            4u => "RightStickHorizontal",
            5u => "LeftTrigger",
            6u => "RightTrigger",
            7u => "FaceSouth",
            8u => "FaceEast",
            10u => "FaceWest",
            11u => "FaceNorth",
            13u => "SystemLeft",
            14u => "SystemRight",
            15u => "SystemCenter",
            17u => "LeftShoulder",
            18u => "RightShoulder",
            19u => "LeftStickPress",
            20u => "RightStickPress",
            23u => "DPadUp",
            24u => "DPadDown",
            25u => "DPadLeft",
            26u => "DPadRight",
            _ => $"ControllerValue:0x{value:X8}"
        };
    }

    public static bool TryGetControllerInputValue(string? storageKey, out uint value)
    {
        value = storageKey switch
        {
            "LeftStickVertical" => 1u,
            "LeftStickHorizontal" => 2u,
            "RightStickVertical" => 3u,
            "RightStickHorizontal" => 4u,
            "LeftTrigger" => 5u,
            "RightTrigger" => 6u,
            "FaceSouth" => 7u,
            "FaceEast" => 8u,
            "FaceWest" => 10u,
            "FaceNorth" => 11u,
            "SystemLeft" => 13u,
            "SystemRight" => 14u,
            "SystemCenter" => 15u,
            "LeftShoulder" => 17u,
            "RightShoulder" => 18u,
            "LeftStickPress" => 19u,
            "RightStickPress" => 20u,
            "DPadUp" => 23u,
            "DPadDown" => 24u,
            "DPadLeft" => 25u,
            "DPadRight" => 26u,
            _ => 0u
        };

        return value != 0u
            || TryParseTaggedUInt32(storageKey, "ControllerValue:", out value);
    }

    public static string GetHelperBindingStorageKey(uint mainValue)
    {
        return mainValue switch
        {
            0x01u => "LeftShoulder",
            0x02u => "RightShoulder",
            0x04u => "LeftTrigger",
            0x08u => "RightTrigger",
            _ => $"HelperValue:0x{mainValue:X8}"
        };
    }

    public static bool TryGetHelperBindingMainValue(string? storageKey, out uint mainValue)
    {
        mainValue = storageKey switch
        {
            "LeftShoulder" => 0x01u,
            "RightShoulder" => 0x02u,
            "LeftTrigger" => 0x04u,
            "RightTrigger" => 0x08u,
            _ => 0u
        };

        return mainValue != 0u
            || TryParseTaggedUInt32(storageKey, "HelperValue:", out mainValue);
    }

    public static string GetActionHelperStorageKey(uint stateValue)
    {
        return stateValue switch
        {
            ActionStateSingle => "Single",
            ActionStateHelper1 => "Helper1",
            ActionStateHelper2 => "Helper2",
            _ => $"ActionState:0x{stateValue:X8}"
        };
    }

    public static bool TryGetActionHelperState(string? storageKey, out uint stateValue)
    {
        stateValue = storageKey switch
        {
            "Single" => ActionStateSingle,
            "Helper1" => ActionStateHelper1,
            "Helper2" => ActionStateHelper2,
            _ => 0u
        };

        return storageKey is "Single" or "Helper1" or "Helper2";
    }

    public static string GetPresetStorageKey(uint value)
    {
        return value switch
        {
            1u => "ConfirmCancelPreset1",
            2u => "ConfirmCancelPreset2",
            3u => "ConfirmCancelPreset3",
            _ => $"ConfirmCancelPreset:0x{value:X8}"
        };
    }

    public static bool TryGetPresetValue(string? storageKey, out uint value)
    {
        value = storageKey switch
        {
            "ConfirmCancelPreset1" => 1u,
            "ConfirmCancelPreset2" => 2u,
            "ConfirmCancelPreset3" => 3u,
            _ => 0u
        };

        return value != 0u
            || TryParseTaggedUInt32(storageKey, "ConfirmCancelPreset:", out value);
    }

    public static string GetKeyMouseInputStorageKey(uint inputType, uint value)
    {
        var typeName = inputType switch
        {
            InputTypeKeyboard => "Keyboard",
            InputTypeMouse => "Mouse",
            _ => $"InputType0x{inputType:X8}"
        };

        if (TryGetKnownKeyMouseStorageName(inputType, value, out var storageName))
        {
            return $"{typeName}:{storageName}";
        }

        return $"{typeName}:0x{value:X8}";
    }

    public static bool TryGetKeyMouseInputValue(
        string? storageKey,
        out uint inputType,
        out uint value)
    {
        inputType = 0u;
        value = 0u;
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            return false;
        }

        var separatorIndex = storageKey.IndexOf(':');
        if (separatorIndex <= 0 || separatorIndex == storageKey.Length - 1)
        {
            return false;
        }

        var typeKey = storageKey[..separatorIndex];
        var valueKey = storageKey[(separatorIndex + 1)..];

        inputType = typeKey switch
        {
            "Keyboard" => InputTypeKeyboard,
            "Mouse" => InputTypeMouse,
            _ => 0u
        };

        if (inputType == InputTypeKeyboard)
        {
            return TryGetKeyboardInputValue(valueKey, out value)
                || TryParsePrefixedHexUInt32(valueKey, out value);
        }

        if (inputType == InputTypeMouse)
        {
            return TryGetMouseInputValue(valueKey, out value)
                || TryParsePrefixedHexUInt32(valueKey, out value);
        }

        if (!TryParseTaggedUInt32(typeKey, "InputType", out inputType))
        {
            return false;
        }

        return TryParsePrefixedHexUInt32(valueKey, out value);
    }

    private static bool TryGetKnownKeyMouseStorageName(
        uint inputType,
        uint value,
        out string storageName)
    {
        if (inputType == InputTypeKeyboard)
        {
            storageName = GetKeyboardStorageName(value) ?? string.Empty;
            return storageName.Length > 0;
        }

        if (inputType == InputTypeMouse)
        {
            storageName = GetMouseStorageName(value) ?? string.Empty;
            return storageName.Length > 0;
        }

        storageName = string.Empty;
        return false;
    }

    private static string? GetKeyboardStorageName(uint value)
    {
        if (value is >= 0x30u and <= 0x39u)
        {
            return ((char)value).ToString();
        }

        if (value is >= 0x61u and <= 0x7Au)
        {
            return char.ToUpperInvariant((char)value).ToString();
        }

        if (value is >= 0x100u and <= 0x109u)
        {
            return $"Num{value - 0x100u}";
        }

        if (value is >= 0x11Au and <= 0x125u)
        {
            return $"F{value - 0x119u}";
        }

        return value switch
        {
            0x9u => "Tab",
            0xDu => "Enter",
            0x1Bu => "Escape",
            0x20u => "Space",
            0x27u => "Colon",
            0x2Cu => "LessThan",
            0x2Du => "Minus",
            0x2Eu => "GreaterThan",
            0x2Fu => "Slash",
            0x3Bu => "Semicolon",
            0x3Du => "Caret",
            0x5Bu => "At",
            0x5Cu => "RightBracket",
            0x5Du => "LeftBracket",
            0x60u => "Tilde",
            0x111u => "ArrowUp",
            0x112u => "ArrowDown",
            0x113u => "ArrowRight",
            0x114u => "ArrowLeft",
            0x12Fu => "RightShift",
            0x130u => "LeftShift",
            0x131u => "RightControl",
            0x132u => "LeftControl",
            0x133u => "RightAlt",
            0x134u => "LeftAlt",
            _ => null
        };
    }

    private static bool TryGetKeyboardInputValue(string storageName, out uint value)
    {
        value = storageName switch
        {
            "Tab" => 0x9u,
            "Enter" => 0xDu,
            "Escape" => 0x1Bu,
            "Space" => 0x20u,
            "Colon" => 0x27u,
            "LessThan" => 0x2Cu,
            "Minus" => 0x2Du,
            "GreaterThan" => 0x2Eu,
            "Slash" => 0x2Fu,
            "Semicolon" => 0x3Bu,
            "Caret" => 0x3Du,
            "At" => 0x5Bu,
            "RightBracket" => 0x5Cu,
            "LeftBracket" => 0x5Du,
            "Tilde" => 0x60u,
            "ArrowUp" => 0x111u,
            "ArrowDown" => 0x112u,
            "ArrowRight" => 0x113u,
            "ArrowLeft" => 0x114u,
            "RightShift" => 0x12Fu,
            "LeftShift" => 0x130u,
            "RightControl" => 0x131u,
            "LeftControl" => 0x132u,
            "RightAlt" => 0x133u,
            "LeftAlt" => 0x134u,
            _ => 0u
        };

        if (value != 0u)
        {
            return true;
        }

        if (storageName.Length == 1)
        {
            var character = storageName[0];
            if (character is >= '0' and <= '9')
            {
                value = character;
                return true;
            }

            if (character is >= 'A' and <= 'Z')
            {
                value = char.ToLowerInvariant(character);
                return true;
            }
        }

        if (storageName.Length == 4
            && storageName.StartsWith("Num", StringComparison.Ordinal)
            && storageName[3] is >= '0' and <= '9')
        {
            value = 0x100u + (uint)(storageName[3] - '0');
            return true;
        }

        if (storageName.Length is 2 or 3
            && storageName[0] == 'F'
            && int.TryParse(storageName[1..], NumberStyles.None, CultureInfo.InvariantCulture, out var functionKey)
            && functionKey is >= 1 and <= 12)
        {
            value = 0x119u + (uint)functionKey;
            return true;
        }

        return false;
    }

    private static string? GetMouseStorageName(uint value)
    {
        return value switch
        {
            0u => "LeftClick",
            1u => "RightClick",
            2u => "MiddleButton",
            3u => "Button3",
            4u => "Button4",
            5u => "Button5",
            6u => "Button6",
            7u => "ScrollWheel",
            _ => null
        };
    }

    private static bool TryGetMouseInputValue(string storageName, out uint value)
    {
        value = storageName switch
        {
            "LeftClick" => 0u,
            "RightClick" => 1u,
            "MiddleButton" => 2u,
            "Button3" => 3u,
            "Button4" => 4u,
            "Button5" => 5u,
            "Button6" => 6u,
            "ScrollWheel" => 7u,
            _ => uint.MaxValue
        };

        return value != uint.MaxValue;
    }

    private static bool TryParsePrefixedHexUInt32(string value, out uint parsed)
    {
        parsed = 0u;
        return value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            && TryParseUInt32(value, out parsed);
    }

    private static bool TryParseTaggedUInt32(string? value, string prefix, out uint parsed)
    {
        parsed = 0u;
        return !string.IsNullOrEmpty(value)
            && value.StartsWith(prefix, StringComparison.Ordinal)
            && TryParseUInt32(value[prefix.Length..], out parsed);
    }

    private static bool TryParseUInt32(string value, out uint parsed)
    {
        const NumberStyles hexadecimalStyles = NumberStyles.AllowHexSpecifier;
        var number = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? value[2..]
            : value;

        return uint.TryParse(
            number,
            hexadecimalStyles,
            CultureInfo.InvariantCulture,
            out parsed);
    }

    private static string GetActionGroupPrefix(KeybindModeGroup group)
    {
        return group switch
        {
            KeybindModeGroup.Main => "main",
            KeybindModeGroup.QuickWheel => "quick_wheel",
            KeybindModeGroup.Photo => "photo",
            KeybindModeGroup.Fishing => "fishing",
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };
    }

    public static bool IsKnownKeyboardLayout(string? keyboardLayout)
    {
        return KeyboardLayouts.Contains(keyboardLayout, StringComparer.OrdinalIgnoreCase);
    }

    public static string NormalizeKeyboardLayout(string? keyboardLayout)
    {
        return IsKnownKeyboardLayout(keyboardLayout)
            ? keyboardLayout!
            : DefaultKeyboardLayout;
    }

    public static IReadOnlyList<ControllerInputOption> GetControllerOptions(
        string? controllerType,
        PluginLocalizer texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var inputLabels = GetControllerInputLabelMap(controllerType);
        return ControllerInputOptions
            .Where(option => inputLabels.ContainsKey(option.Value))
            .Select(option => CreateControllerInputOption(
                controllerType,
                option.Value,
                inputLabels[option.Value],
                texts))
            .ToArray();
    }

    public static IReadOnlyList<HelperBindingOption> GetHelperOptions(
        string? controllerType,
        PluginLocalizer texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        return new[]
        {
            CreateHelperBindingOption(
                controllerType,
                0x01u,
                17u,
                GetControllerInputDisplayLabel(controllerType, 17u, texts),
                texts),
            CreateHelperBindingOption(
                controllerType,
                0x02u,
                18u,
                GetControllerInputDisplayLabel(controllerType, 18u, texts),
                texts),
            CreateHelperBindingOption(
                controllerType,
                0x04u,
                5u,
                GetControllerInputDisplayLabel(controllerType, 5u, texts),
                texts),
            CreateHelperBindingOption(
                controllerType,
                0x08u,
                6u,
                GetControllerInputDisplayLabel(controllerType, 6u, texts),
                texts)
        };
    }

    public static IReadOnlyList<PresetOption> GetPresetOptions(
        string? controllerType,
        PluginLocalizer texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var resolvedType = ControllerTypes.Contains(controllerType, StringComparer.OrdinalIgnoreCase)
            ? controllerType!
            : DefaultControllerType;

        return new[]
        {
            CreatePresetOption(resolvedType, 0x1u, 10u, 7u, texts),
            CreatePresetOption(resolvedType, 0x2u, 7u, 8u, texts),
            CreatePresetOption(resolvedType, 0x3u, 8u, 7u, texts)
        };
    }

    public static IReadOnlyList<ControllerInputOption> GetAllowedControllerOptions(
        ControllerActionDefinition definition,
        string? controllerType,
        ISet<uint>? blockedValues,
        PluginLocalizer texts)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(texts);

        var allowedValues = definition.AllowedValues.Count > 0
            ? definition.AllowedValues
            : ControllerInputOptions.Select(option => option.Value).ToArray();

        var inputLabels = GetControllerInputLabelMap(controllerType);
        return allowedValues
            .Where(inputLabels.ContainsKey)
            .Where(value => definition.HasExplicitAllowedValues
                || !definition.UsesHelper
                || blockedValues is null
                || !blockedValues.Contains(value))
            .Select(value => CreateControllerInputOption(
                controllerType,
                value,
                inputLabels[value],
                texts))
            .ToArray();
    }

    public static IReadOnlyList<KeyMouseInputOption> GetAllowedKeyMouseOptions(
        KeyMouseActionDefinition definition,
        string? keyboardLayout)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var candidateOptions = definition.AllowedInputTypes.Count == 0
            ? KeyMouseInputOptions
            : KeyMouseInputOptions.Where(option => definition.AllowedInputTypes.Contains(option.InputType));

        return candidateOptions
            .Select(option => CreateKeyMouseInputOption(
                option.InputType,
                option.Value,
                GetKeyMouseInputDisplayLabel(keyboardLayout, option)))
            .ToArray();
    }

    public static KeyMouseInputOption? CreateKnownKeyMouseInputOption(
        uint inputType,
        uint value,
        string? keyboardLayout)
    {
        var option = KeyMouseInputOptions.FirstOrDefault(candidate =>
            candidate.InputType == inputType && candidate.Value == value);
        return option is null
            ? null
            : CreateKeyMouseInputOption(
                option.InputType,
                option.Value,
                GetKeyMouseInputDisplayLabel(keyboardLayout, option));
    }

    public static bool UsesLControlPrefix(KeyMouseActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return KeyMouseLControlPrefixActionIds.Contains(definition.Id);
    }

    public static string GetControllerInputDisplayLabel(
        string? controllerType,
        uint value,
        PluginLocalizer texts)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var inputLabels = GetControllerInputLabelMap(controllerType);
        return inputLabels.TryGetValue(value, out var label)
            ? label
            : texts.Format("Keybind.Value.UnknownControllerInput", value);
    }

    private static IReadOnlyDictionary<uint, string> GetControllerInputLabelMap(string? controllerType)
    {
        return ControllerInputLabels.TryGetValue(controllerType ?? string.Empty, out var map)
            ? map
            : ControllerInputLabels[DefaultControllerType];
    }

    private static string GetKeyMouseInputDisplayLabel(
        string? keyboardLayout,
        KeyMouseInputOption option)
    {
        if (option.InputType == InputTypeKeyboard
            && KeyboardInputLabelOverrides.TryGetValue(
                NormalizeKeyboardLayout(keyboardLayout),
                out var overrides)
            && overrides.TryGetValue(option.Value, out var label))
        {
            return label;
        }

        return option.Label;
    }

    public static IReadOnlyList<ControllerActionDefinition> GetControllerActions(KeybindModeGroup group)
    {
        return group switch
        {
            KeybindModeGroup.Main => ControllerMainActions,
            KeybindModeGroup.QuickWheel => ControllerQuickWheelActions,
            KeybindModeGroup.Photo => ControllerPhotoActions,
            KeybindModeGroup.Fishing => ControllerFishingActions,
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };
    }

    public static IReadOnlyList<KeyMouseActionDefinition> GetKeyMouseActions(KeybindModeGroup group)
    {
        return group switch
        {
            KeybindModeGroup.Main => KeyMouseMainActions,
            KeybindModeGroup.QuickWheel => KeyMouseQuickWheelActions,
            KeybindModeGroup.Photo => KeyMousePhotoActions,
            KeybindModeGroup.Fishing => KeyMouseFishingActions,
            _ => throw new ArgumentOutOfRangeException(nameof(group))
        };
    }

    private static PresetOption CreatePresetOption(
        string controllerType,
        uint presetValue,
        uint confirmButtonValue,
        uint cancelButtonValue,
        PluginLocalizer texts)
    {
        var confirmLabel = GetControllerInputDisplayLabel(
            controllerType,
            confirmButtonValue,
            texts);
        var cancelLabel = GetControllerInputDisplayLabel(
            controllerType,
            cancelButtonValue,
            texts);

        return new PresetOption(presetValue, $"{confirmLabel} / {cancelLabel}")
        {
            ConfirmVisual = CreateControllerInputVisual(
                controllerType,
                confirmButtonValue,
                confirmLabel,
                texts),
            CancelVisual = CreateControllerInputVisual(
                controllerType,
                cancelButtonValue,
                cancelLabel,
                texts)
        };
    }

    public static ControllerInputOption CreateControllerInputOption(
        string? controllerType,
        uint value,
        string label,
        PluginLocalizer texts)
    {
        return new ControllerInputOption(value, label)
        {
            Visual = CreateControllerInputVisual(controllerType, value, label, texts)
        };
    }

    private static HelperBindingOption CreateHelperBindingOption(
        string? controllerType,
        uint mainValue,
        uint controllerValue,
        string label,
        PluginLocalizer texts)
    {
        return new HelperBindingOption(mainValue, label)
        {
            Visual = CreateControllerInputVisual(controllerType, controllerValue, label, texts)
        };
    }

    public static KeyMouseInputOption CreateKeyMouseInputOption(
        uint inputType,
        uint value,
        string label)
    {
        return new KeyMouseInputOption(inputType, value, label)
        {
            Visual = CreateKeyMouseInputVisual(inputType, value, label)
        };
    }

    private static KeybindInputVisual CreateControllerInputVisual(
        string? controllerType,
        uint value,
        string label,
        PluginLocalizer texts)
    {
        var commonVisual = GetCommonControllerVisual(value);
        if (commonVisual is not null)
        {
            return commonVisual;
        }

        if (string.Equals(controllerType, "Nintendo", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                1u => CreateAssetVisual(1009, texts["Keybind.Input.Axis.ForwardBack"]),
                2u => CreateAssetVisual(1009, texts["Keybind.Input.Axis.LeftRight"]),
                3u => CreateAssetVisual(1011, texts["Keybind.Input.Axis.ForwardBack"]),
                4u => CreateAssetVisual(1011, texts["Keybind.Input.Axis.LeftRight"]),
                5u => CreateAssetVisual(2016),
                6u => CreateAssetVisual(2017),
                7u => CreateAssetVisual(2002),
                8u => CreateAssetVisual(2001),
                10u => CreateAssetVisual(2004),
                11u => CreateAssetVisual(1001),
                13u => CreateAssetVisual(2021),
                14u => CreateAssetVisual(2020),
                15u => CreateAssetVisual(2022),
                17u => CreateAssetVisual(2018),
                18u => CreateAssetVisual(2019),
                19u => CreateAssetVisual(2010),
                20u => CreateAssetVisual(2012),
                _ => KeybindInputVisual.TextOnly(label)
            };
        }

        if (string.Equals(controllerType, "Xbox", StringComparison.OrdinalIgnoreCase))
        {
            return value switch
            {
                1u => CreateAssetVisual(2009, texts["Keybind.Input.Axis.ForwardBack"]),
                2u => CreateAssetVisual(2009, texts["Keybind.Input.Axis.LeftRight"]),
                3u => CreateAssetVisual(2011, texts["Keybind.Input.Axis.ForwardBack"]),
                4u => CreateAssetVisual(2011, texts["Keybind.Input.Axis.LeftRight"]),
                5u => CreateAssetVisual(2007),
                6u => CreateAssetVisual(2008),
                7u => CreateAssetVisual(2001),
                8u => CreateAssetVisual(2002),
                10u => CreateAssetVisual(1001),
                11u => CreateAssetVisual(2004),
                13u => CreateAssetVisual(2014),
                14u => CreateAssetVisual(2013),
                15u => CreateAssetVisual(2015),
                17u => CreateAssetVisual(2005),
                18u => CreateAssetVisual(2006),
                19u => CreateAssetVisual(2010),
                20u => CreateAssetVisual(2012),
                _ => KeybindInputVisual.TextOnly(label)
            };
        }

        return value switch
        {
            1u => CreateAssetVisual(1009, texts["Keybind.Input.Axis.ForwardBack"]),
            2u => CreateAssetVisual(1009, texts["Keybind.Input.Axis.LeftRight"]),
            3u => CreateAssetVisual(1011, texts["Keybind.Input.Axis.ForwardBack"]),
            4u => CreateAssetVisual(1011, texts["Keybind.Input.Axis.LeftRight"]),
            5u => CreateAssetVisual(1007),
            6u => CreateAssetVisual(1008),
            7u => CreateAssetVisual(2003),
            8u => CreateAssetVisual(1002),
            10u => CreateAssetVisual(1003),
            11u => CreateAssetVisual(1004),
            13u => CreateAssetVisual(1020),
            14u => CreateAssetVisual(1013),
            15u => CreateAssetVisual(1014),
            17u => CreateAssetVisual(1005),
            18u => CreateAssetVisual(1006),
            19u => CreateAssetVisual(1010),
            20u => CreateAssetVisual(1012),
            _ => KeybindInputVisual.TextOnly(label)
        };
    }

    private static KeybindInputVisual? GetCommonControllerVisual(uint value)
    {
        return value switch
        {
            23u => CreateAssetVisual(1016),
            24u => CreateAssetVisual(1017),
            25u => CreateAssetVisual(1018),
            26u => CreateAssetVisual(1019),
            _ => null
        };
    }

    private static KeybindInputVisual CreateKeyMouseInputVisual(
        uint inputType,
        uint value,
        string label)
    {
        if (inputType == InputTypeMouse)
        {
            return value switch
            {
                0u => CreateAssetVisual(323),
                1u => CreateAssetVisual(324),
                2u => CreateAssetVisual(326),
                3u => CreateAssetVisual(320, "3"),
                4u => CreateAssetVisual(320, "4"),
                5u => CreateAssetVisual(320, "5"),
                6u => CreateAssetVisual(320, "6"),
                7u => CreateAssetVisual(325),
                _ => KeybindInputVisual.TextOnly(label)
            };
        }

        return KeybindInputVisual.TextOnly(label);
    }

    private static KeybindInputVisual CreateAssetVisual(int assetFileName, string? text = null)
    {
        return new KeybindInputVisual(
            $"pack://application:,,,/KeybindTool;component/Assets/{assetFileName}.png",
            text);
    }

    private static ControllerActionDefinition CreateControllerAction(
        KeybindModeGroup group,
        string key,
        IReadOnlyList<int> relativeOffsets,
        IReadOnlyList<uint>? allowedValues = null,
        bool usesHelper = true)
    {
        return new ControllerActionDefinition(
            GetActionId(group, key),
            group,
            key,
            relativeOffsets,
            allowedValues ?? Array.Empty<uint>(),
            allowedValues is not null,
            usesHelper);
    }

    private static KeyMouseActionDefinition CreateKeyMouseAction(
        KeybindModeGroup group,
        string key,
        IReadOnlyList<int> relativeOffsets,
        IReadOnlyList<uint>? allowedInputTypes = null)
    {
        return new KeyMouseActionDefinition(
            GetActionId(group, key),
            group,
            key,
            relativeOffsets,
            allowedInputTypes ?? Array.Empty<uint>());
    }

    private static IReadOnlyDictionary<string, string> CreateDirectLink(
        KeybindModeGroup modeGroup,
        string modeKey,
        KeybindModeGroup sourceGroup,
        string sourceKey)
    {
        return new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GetActionId(modeGroup, modeKey)] = GetActionId(sourceGroup, sourceKey)
            });
    }

    private static IReadOnlyDictionary<string, string> BuildModeLinks<TDefinition>(
        IReadOnlyList<TDefinition> normalActions,
        IReadOnlyList<TDefinition> modeActions,
        IReadOnlyDictionary<string, string> exceptionNormalToMode,
        ISet<string> unlinkedModeKeys)
        where TDefinition : IKeybindActionDefinition
    {
        var normalIdsByKey = normalActions.ToDictionary(
            action => action.Key,
            action => action.Id,
            StringComparer.Ordinal);
        var modeIdsByKey = modeActions.ToDictionary(
            action => action.Key,
            action => action.Id,
            StringComparer.Ordinal);

        var links = new Dictionary<string, string>(StringComparer.Ordinal);
        var exceptionModeKeys = exceptionNormalToMode.Values.ToHashSet(StringComparer.Ordinal);

        foreach (var (modeKey, modeId) in modeIdsByKey)
        {
            if (unlinkedModeKeys.Contains(modeKey) || exceptionModeKeys.Contains(modeKey))
            {
                continue;
            }

            if (normalIdsByKey.TryGetValue(modeKey, out var sourceId))
            {
                links[modeId] = sourceId;
            }
        }

        foreach (var (normalKey, modeKey) in exceptionNormalToMode)
        {
            if (normalIdsByKey.TryGetValue(normalKey, out var sourceId)
                && modeIdsByKey.TryGetValue(modeKey, out var modeId))
            {
                links[modeId] = sourceId;
            }
        }

        return new ReadOnlyDictionary<string, string>(links);
    }
}
