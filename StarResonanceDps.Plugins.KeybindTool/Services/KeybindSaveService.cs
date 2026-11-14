using System.Buffers.Binary;
using System.IO;
using System.IO.Compression;
using System.Text;
using StarResonanceDps.Plugins.KeybindTool.Models;
using StarResonanceDps.PluginSdk;

namespace StarResonanceDps.Plugins.KeybindTool.Services;

internal sealed class KeybindSaveService
{
    private readonly PluginLocalizer _texts;

    private static readonly byte[] InputAnchor = Encoding.ASCII.GetBytes("BKRInputConfigData");
    private static readonly byte[] PresetAnchor = Encoding.ASCII.GetBytes("BKL_SETID_7001");
    private static readonly byte[] HelperPetWheelAnchor = Encoding.ASCII.GetBytes("PetWheel");

    private static readonly InputSectionDefinition[] InputSectionDefinitions =
    {
        new("play", Encoding.ASCII.GetBytes("Play"), 0x00036),
        new("ui", Encoding.ASCII.GetBytes("UI"), 0x0129C),
        new("take_photo_common", Encoding.ASCII.GetBytes("TakePhotoCommon"), 0x02015),
        new("expression", Encoding.ASCII.GetBytes("Expression"), 0x02859),
        new("fishing", Encoding.ASCII.GetBytes("Fishing"), 0x02940),
        new("default", Encoding.ASCII.GetBytes("Default"), 0x02D03),
        new("band_performance", Encoding.ASCII.GetBytes("BandPerformance"), 0x02D3E)
    };

    private const int PresetRelativeOffset = 0x17;

    public KeybindSaveService(PluginLocalizer texts)
    {
        _texts = texts ?? throw new ArgumentNullException(nameof(texts));
    }

    public KeybindSaveSession Load(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var raw = File.ReadAllBytes(filePath);
        var data = Decompress(raw);

        var inputAnchorOffset = FindAnchor(data, InputAnchor, _texts["Keybind.Error.RequiredInputDataNotFound"]);
        var inputSectionAnchorOffsets = FindInputSectionAnchorOffsets(data, inputAnchorOffset);
        var (helper1Offset, helper2Offset) = FindHelperOffsets(data, inputAnchorOffset);

        var presetAnchorOffset = FindAnchorOrNegative(data, PresetAnchor);
        var presetOffset = presetAnchorOffset >= 0
            ? presetAnchorOffset + PresetRelativeOffset
            : (int?)null;

        ValidateSessionLayout(
            data,
            inputSectionAnchorOffsets,
            helper1Offset,
            helper2Offset,
            presetOffset);

        return new KeybindSaveSession(
            filePath,
            data,
            inputAnchorOffset,
            inputSectionAnchorOffsets,
            helper1Offset,
            helper2Offset,
            presetOffset);
    }

    public IReadOnlyList<int> GetControllerOffsets(
        KeybindSaveSession session,
        ControllerActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);

        return GetOffsets(
            session.InputSectionAnchorOffsets,
            definition.RelativeOffsets);
    }

    public IReadOnlyList<int> GetKeyMouseOffsets(
        KeybindSaveSession session,
        KeyMouseActionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(definition);

        return GetOffsets(
            session.InputSectionAnchorOffsets,
            definition.RelativeOffsets);
    }

    public KeybindWriteTargetValidation ValidateWriteTargets(KeybindSaveSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var invalidTargets = new List<KeybindInvalidWriteTarget>();
        var controllerActionIdsToSkip = new HashSet<string>(StringComparer.Ordinal);
        var keyMouseActionIdsToSkip = new HashSet<string>(StringComparer.Ordinal);

        foreach (var definition in KeybindCatalog.ControllerActions)
        {
            var invalidOffsets = GetControllerInvalidWriteOffsets(session, definition);
            if (invalidOffsets.Count == 0)
            {
                continue;
            }

            invalidTargets.Add(new KeybindInvalidWriteTarget(
                KeybindSaveTargetDevice.Controller,
                definition.Id,
                definition.Group,
                definition.Key,
                invalidOffsets));
            controllerActionIdsToSkip.Add(definition.Id);
        }

        foreach (var definition in KeybindCatalog.KeyMouseActions)
        {
            var invalidOffsets = GetKeyMouseInvalidWriteOffsets(session, definition);
            if (invalidOffsets.Count == 0)
            {
                continue;
            }

            invalidTargets.Add(new KeybindInvalidWriteTarget(
                KeybindSaveTargetDevice.KeyMouse,
                definition.Id,
                definition.Group,
                definition.Key,
                invalidOffsets));
            keyMouseActionIdsToSkip.Add(definition.Id);
        }

        return new KeybindWriteTargetValidation(
            invalidTargets,
            controllerActionIdsToSkip,
            keyMouseActionIdsToSkip);
    }

    public uint ReadUInt32(KeybindSaveSession session, int offset)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ReadUInt32(session.Data, offset);
    }

    public uint ReadInputType(KeybindSaveSession session, int valueOffset)
    {
        ArgumentNullException.ThrowIfNull(session);
        return ReadUInt32(session.Data, valueOffset - sizeof(uint));
    }

    public void WriteUInt32(byte[] data, int offset, uint value)
    {
        ArgumentNullException.ThrowIfNull(data);

        EnsureRange(data, offset, sizeof(uint));
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)), value);
    }

    public void Save(string filePath, byte[] data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(data);

        var directory = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(_texts["Keybind.Error.SaveDirectoryUnavailable"]);
        }

        var temporaryPath = $"{filePath}.tmp";
        try
        {
            var compressed = Compress(data);
            File.WriteAllBytes(temporaryPath, compressed);
            File.Move(temporaryPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private IReadOnlyList<KeybindInvalidWriteOffset> GetControllerInvalidWriteOffsets(
        KeybindSaveSession session,
        ControllerActionDefinition definition)
    {
        var invalidOffsets = new List<KeybindInvalidWriteOffset>();
        foreach (var offset in GetControllerOffsets(session, definition))
        {
            if (!IsControllerActionRecord(session.Data, offset))
            {
                invalidOffsets.Add(CreateInvalidWriteOffset(
                    session,
                    offset,
                    controller: true));
            }
        }

        return invalidOffsets;
    }

    private IReadOnlyList<KeybindInvalidWriteOffset> GetKeyMouseInvalidWriteOffsets(
        KeybindSaveSession session,
        KeyMouseActionDefinition definition)
    {
        var invalidOffsets = new List<KeybindInvalidWriteOffset>();
        foreach (var offset in GetKeyMouseOffsets(session, definition))
        {
            if (!IsKeyMouseActionRecord(session.Data, offset))
            {
                invalidOffsets.Add(CreateInvalidWriteOffset(
                    session,
                    offset,
                    controller: false));
            }
        }

        return invalidOffsets;
    }

    private static bool IsControllerActionRecord(byte[] data, int valueOffset)
    {
        if (!HasRange(data, valueOffset - sizeof(uint), sizeof(uint) * 3))
        {
            return false;
        }

        var inputType = ReadUInt32Unchecked(data, valueOffset - sizeof(uint));
        var stateValue = ReadUInt32Unchecked(data, valueOffset + sizeof(uint));
        return inputType == KeybindCatalog.InputTypeController
            && (stateValue is KeybindCatalog.ActionStateSingle
                or KeybindCatalog.ActionStateHelper1
                or KeybindCatalog.ActionStateHelper2);
    }

    private static bool IsKeyMouseActionRecord(byte[] data, int valueOffset)
    {
        if (!HasRange(data, valueOffset - sizeof(uint), sizeof(uint) * 2))
        {
            return false;
        }

        var inputType = ReadUInt32Unchecked(data, valueOffset - sizeof(uint));
        return inputType is KeybindCatalog.InputTypeKeyboard
            or KeybindCatalog.InputTypeMouse;
    }

    private static KeybindInvalidWriteOffset CreateInvalidWriteOffset(
        KeybindSaveSession session,
        int valueOffset,
        bool controller)
    {
        var relativeOffset = valueOffset - session.InputAnchorOffset;
        var inputType = HasRange(session.Data, valueOffset - sizeof(uint), sizeof(uint))
            ? ReadUInt32Unchecked(session.Data, valueOffset - sizeof(uint))
            : 0u;
        uint? stateValue = controller
            && HasRange(session.Data, valueOffset + sizeof(uint), sizeof(uint))
            ? ReadUInt32Unchecked(session.Data, valueOffset + sizeof(uint))
            : null;

        return new KeybindInvalidWriteOffset(relativeOffset, inputType, stateValue);
    }

    private static byte[] Decompress(byte[] raw)
    {
        using var input = new MemoryStream(raw, writable: false);
        using var brotli = new BrotliStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        brotli.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] Compress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            brotli.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    private static int FindAnchor(byte[] data, byte[] anchor, string errorMessage)
    {
        var offset = FindAnchorOrNegative(data, anchor);
        if (offset < 0)
        {
            throw new InvalidDataException(errorMessage);
        }

        return offset;
    }

    private static int FindAnchorOrNegative(byte[] data, byte[] anchor)
    {
        return FindAnchorOrNegative(data, anchor, 0);
    }

    private static int FindAnchorOrNegative(byte[] data, byte[] anchor, int searchOffset)
    {
        if (anchor.Length == 0 || data.Length < anchor.Length)
        {
            return -1;
        }

        var startOffset = Math.Max(0, searchOffset);
        for (var offset = startOffset; offset <= data.Length - anchor.Length; offset++)
        {
            if (data.AsSpan(offset, anchor.Length).SequenceEqual(anchor))
            {
                return offset;
            }
        }

        return -1;
    }

    private IReadOnlyDictionary<string, int> FindInputSectionAnchorOffsets(
        byte[] data,
        int inputAnchorOffset)
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var searchOffset = inputAnchorOffset;

        foreach (var section in InputSectionDefinitions)
        {
            var offset = FindAnchorOrNegative(data, section.Anchor, searchOffset);
            if (offset < 0)
            {
                throw new InvalidDataException(_texts["Keybind.Error.UnexpectedFileFormat"]);
            }

            positions[section.Id] = offset;
            searchOffset = offset + section.Anchor.Length;
        }

        return positions;
    }

    private (int Helper1Offset, int Helper2Offset) FindHelperOffsets(
        byte[] data,
        int inputAnchorOffset)
    {
        var petWheelOffset = FindAnchorOrNegative(data, HelperPetWheelAnchor, inputAnchorOffset);
        if (petWheelOffset < 0)
        {
            throw new InvalidDataException(_texts["Keybind.Error.HelperKeysNotFound"]);
        }

        var helper1Offset = data.Length - (sizeof(uint) * 2);
        var helper2Offset = data.Length - sizeof(uint);
        if (helper1Offset <= petWheelOffset
            || !HasRange(data, helper1Offset, sizeof(uint))
            || !HasRange(data, helper2Offset, sizeof(uint)))
        {
            throw new InvalidDataException(_texts["Keybind.Error.HelperKeysNotFound"]);
        }

        var helper1Value = ReadUInt32(data, helper1Offset);
        var helper2Value = ReadUInt32(data, helper2Offset);

        if (!IsKnownHelperValue(helper1Value))
        {
            throw new InvalidDataException(_texts["Keybind.Error.Helper1OffsetNotFound"]);
        }

        if (!IsKnownHelperValue(helper2Value))
        {
            throw new InvalidDataException(_texts["Keybind.Error.Helper2OffsetNotFound"]);
        }

        return (helper1Offset, helper2Offset);
    }

    private void ValidateSessionLayout(
        byte[] data,
        IReadOnlyDictionary<string, int> inputSectionAnchorOffsets,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        foreach (var action in KeybindCatalog.ControllerActions)
        {
            foreach (var offset in GetOffsets(inputSectionAnchorOffsets, action.RelativeOffsets))
            {
                EnsureRange(
                    data,
                    offset - sizeof(uint),
                    sizeof(uint) * 3);
            }
        }

        foreach (var action in KeybindCatalog.KeyMouseActions)
        {
            foreach (var offset in GetOffsets(inputSectionAnchorOffsets, action.RelativeOffsets))
            {
                EnsureRange(
                    data,
                    offset - sizeof(uint),
                    sizeof(uint) * 2);
            }
        }

        EnsureRange(data, helper1Offset, sizeof(uint));
        EnsureRange(data, helper2Offset, sizeof(uint));

        if (presetOffset is not null)
        {
            EnsureRange(data, presetOffset.Value, sizeof(byte));
        }
    }

    private IReadOnlyList<int> GetOffsets(
        IReadOnlyDictionary<string, int> inputSectionAnchorOffsets,
        IReadOnlyList<int> legacyRelativeOffsets)
    {
        return legacyRelativeOffsets
            .Select(relativeOffset => ResolveInputOffset(inputSectionAnchorOffsets, relativeOffset))
            .ToArray();
    }

    private int ResolveInputOffset(
        IReadOnlyDictionary<string, int> inputSectionAnchorOffsets,
        int legacyRelativeOffset)
    {
        var section = InputSectionDefinitions[0];
        foreach (var candidate in InputSectionDefinitions)
        {
            if (legacyRelativeOffset < candidate.LegacyRelativeOffset)
            {
                break;
            }

            section = candidate;
        }

        if (!inputSectionAnchorOffsets.TryGetValue(section.Id, out var sectionAnchorOffset))
        {
            throw new InvalidDataException(_texts["Keybind.Error.UnexpectedFileFormat"]);
        }

        return sectionAnchorOffset + legacyRelativeOffset - section.LegacyRelativeOffset;
    }

    private static bool IsKnownHelperValue(uint value)
    {
        return KeybindCatalog.HelperMainToActionValue.ContainsKey(value);
    }

    private uint ReadUInt32(byte[] data, int offset)
    {
        EnsureRange(data, offset, sizeof(uint));
        return ReadUInt32Unchecked(data, offset);
    }

    private static uint ReadUInt32Unchecked(byte[] data, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, sizeof(uint)));
    }

    private static bool HasRange(byte[] data, int offset, int length)
    {
        return offset >= 0 && length >= 0 && offset <= data.Length - length;
    }

    private void EnsureRange(byte[] data, int offset, int length)
    {
        if (!HasRange(data, offset, length))
        {
            throw new InvalidDataException(_texts["Keybind.Error.UnexpectedFileFormat"]);
        }
    }
}

internal sealed record InputSectionDefinition(
    string Id,
    byte[] Anchor,
    int LegacyRelativeOffset);

internal sealed class KeybindSaveSession
{
    public KeybindSaveSession(
        string filePath,
        byte[] data,
        int inputAnchorOffset,
        IReadOnlyDictionary<string, int> inputSectionAnchorOffsets,
        int helper1Offset,
        int helper2Offset,
        int? presetOffset)
    {
        FilePath = filePath;
        Data = data;
        InputAnchorOffset = inputAnchorOffset;
        InputSectionAnchorOffsets = inputSectionAnchorOffsets;
        Helper1Offset = helper1Offset;
        Helper2Offset = helper2Offset;
        PresetOffset = presetOffset;
    }

    public string FilePath { get; }

    public byte[] Data { get; private set; }

    public int InputAnchorOffset { get; }

    public IReadOnlyDictionary<string, int> InputSectionAnchorOffsets { get; }

    public int Helper1Offset { get; }

    public int Helper2Offset { get; }

    public int? PresetOffset { get; }

    public bool IsPresetSupported => PresetOffset is not null;

    public void ReplaceData(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
    }
}
