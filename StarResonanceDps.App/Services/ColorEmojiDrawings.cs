// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Windows;
using System.Windows.Media;
using Serilog;

namespace StarResonanceDps.App.Services;

/// <summary>色つきの絵文字の絵と、行の中で占める大きさ。<see cref="Baseline"/> は上端から文字の基準の線までの高さ。</summary>
public sealed record ColorEmojiDrawing(Drawing Drawing, Size Size, double Baseline);

/// <summary>
/// 絵文字を、Windows の絵文字の書体(Segoe UI Emoji)の色の層を重ねて色つきで描く絵。WPF の文字の描き方は書体の色の情報を使わず、
/// 文字の色の単色になるため、層を1枚ずつその色で描く。
///
/// <para>
/// 書体の色の層(COLR 表の版0の層の記録と、CPAL 表の最初の色の組)は、初めて使うときに1回だけ読む。
/// 絵は「絵文字・大きさ・文字の色・画面の倍率」ごとに1回だけ作って固め、使い回す。画面のスレッドだけで使う。
/// 書体が無い・色の層が無い絵文字は作れない(null)ので、呼ぶ側が普通の文字(単色)で出す。作れなかったことは書体と絵文字ごとに1回ログに残す。
/// </para>
/// </summary>
public static class ColorEmojiDrawings
{
    private const string FontFamilyName = "Segoe UI Emoji";

    /// <summary>層の色の番号のうち、文字の色で描く印。</summary>
    private const ushort ForegroundPaletteIndex = 0xFFFF;

    private static readonly Dictionary<(string Emoji, double EmSize, Color Foreground, double PixelsPerDip), ColorEmojiDrawing?> Drawings = [];
    private static readonly HashSet<string> LoggedMissingEmojis = new(StringComparer.Ordinal);
    private static ColorFont? _font;
    private static bool _isFontLoaded;

    public static ColorEmojiDrawing? Get(string emoji, double emSize, Color foreground, double pixelsPerDip)
    {
        var key = (emoji, emSize, foreground, pixelsPerDip);
        if (Drawings.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!_isFontLoaded)
        {
            _font = LoadFont();
            _isFontLoaded = true;
        }

        var drawing = _font is null ? null : Create(_font, emoji, emSize, foreground, pixelsPerDip);
        Drawings[key] = drawing;
        return drawing;
    }

    private static ColorEmojiDrawing? Create(ColorFont font, string emoji, double emSize, Color foreground, double pixelsPerDip)
    {
        var codePoint = char.ConvertToUtf32(emoji, 0);
        if (!font.GlyphTypeface.CharacterToGlyphMap.TryGetValue(codePoint, out var baseGlyph)
            || !font.Layers.TryGetValue(baseGlyph, out var layers))
        {
            if (LoggedMissingEmojis.Add(emoji))
            {
                Log.Warning("{Font} has no color layers for U+{CodePoint:X}; the emoji is drawn as plain text", FontFamilyName, codePoint);
            }

            return null;
        }

        var foregroundBrush = new SolidColorBrush(foreground);
        foregroundBrush.Freeze();
        var advance = font.GlyphTypeface.AdvanceWidths[baseGlyph] * emSize;
        var baseline = font.GlyphTypeface.Baseline * emSize;
        var group = new DrawingGroup();
        using (var context = group.Open())
        {
            foreach (var (glyph, paletteIndex) in layers)
            {
                var run = new GlyphRun(
                    font.GlyphTypeface,
                    0,
                    false,
                    emSize,
                    (float)pixelsPerDip,
                    [glyph],
                    new Point(0, baseline),
                    [advance],
                    null,
                    null,
                    null,
                    null,
                    null,
                    null);
                context.DrawGlyphRun(GetLayerBrush(font, paletteIndex, foregroundBrush, emoji), run);
            }
        }

        group.Freeze();
        return new ColorEmojiDrawing(group, new Size(advance, font.GlyphTypeface.Height * emSize), baseline);
    }

    /// <summary>層の色。色の組に無い番号は文字の色で描き、絵文字ごとに1回ログに残す。</summary>
    private static Brush GetLayerBrush(ColorFont font, ushort paletteIndex, Brush foregroundBrush, string emoji)
    {
        if (paletteIndex == ForegroundPaletteIndex)
        {
            return foregroundBrush;
        }

        if (paletteIndex < font.PaletteBrushes.Length)
        {
            return font.PaletteBrushes[paletteIndex];
        }

        if (LoggedMissingEmojis.Add(emoji))
        {
            Log.Warning("{Font} has a color layer with palette index {PaletteIndex} outside the palette for {Emoji}; the layer is drawn in the text color",
                FontFamilyName, paletteIndex, emoji);
        }

        return foregroundBrush;
    }

    /// <summary>書体と色の層を読む。書体が無い・色の表が無い・表が読めないときは null(ログに残す)。</summary>
    private static ColorFont? LoadFont()
    {
        var typeface = new Typeface(new FontFamily(FontFamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphTypeface))
        {
            Log.Warning("{Font} is not available; emoji are drawn as plain text", FontFamilyName);
            return null;
        }

        byte[] data;
        using (var stream = glyphTypeface.GetFontStream())
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            data = memory.ToArray();
        }

        // 書体を束ねたファイル(ttcf)は、どの書体の表かを WPF から取れないので読まない(Segoe UI Emoji は1書体のファイル)。
        if (data.Length >= 4 && data[0] == 't' && data[1] == 't' && data[2] == 'c' && data[3] == 'f')
        {
            Log.Warning("{Font} ({Uri}) is a font collection; emoji are drawn as plain text", FontFamilyName, glyphTypeface.FontUri);
            return null;
        }

        try
        {
            var tables = ReadTables(data);
            if (!tables.TryGetValue("COLR", out var colr) || !tables.TryGetValue("CPAL", out var cpal))
            {
                Log.Warning("{Font} ({Uri}) has no color tables; emoji are drawn as plain text", FontFamilyName, glyphTypeface.FontUri);
                return null;
            }

            return new ColorFont(glyphTypeface, ReadLayers(data, colr), ReadPalette(data, cpal));
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            Log.Error(ex, "Failed to read the color tables of {Font} ({Uri}); emoji are drawn as plain text", FontFamilyName, glyphTypeface.FontUri);
            return null;
        }
    }

    /// <summary>書体の表の場所(表の名前 → 書体のデータの中の位置)。</summary>
    private static Dictionary<string, int> ReadTables(byte[] data)
    {
        var tables = new Dictionary<string, int>(StringComparer.Ordinal);
        int count = ReadUInt16(data, 4);
        for (var index = 0; index < count; index++)
        {
            var record = 12 + index * 16;
            tables[System.Text.Encoding.ASCII.GetString(data, record, 4)] = (int)ReadUInt32(data, record + 8);
        }

        return tables;
    }

    /// <summary>COLR 表の版0: 基の字ごとの層(層の字と色の番号)。版1の表も版0の記録を持つ。</summary>
    private static Dictionary<ushort, (ushort Glyph, ushort PaletteIndex)[]> ReadLayers(byte[] data, int colr)
    {
        int baseCount = ReadUInt16(data, colr + 2);
        var baseRecords = colr + (int)ReadUInt32(data, colr + 4);
        var layerRecords = colr + (int)ReadUInt32(data, colr + 8);
        var layers = new Dictionary<ushort, (ushort, ushort)[]>(baseCount);
        for (var index = 0; index < baseCount; index++)
        {
            var record = baseRecords + index * 6;
            int first = ReadUInt16(data, record + 2);
            var glyphLayers = new (ushort, ushort)[ReadUInt16(data, record + 4)];
            for (var layer = 0; layer < glyphLayers.Length; layer++)
            {
                var layerRecord = layerRecords + (first + layer) * 4;
                glyphLayers[layer] = (ReadUInt16(data, layerRecord), ReadUInt16(data, layerRecord + 2));
            }

            layers[ReadUInt16(data, record)] = glyphLayers;
        }

        return layers;
    }

    /// <summary>CPAL 表の最初の色の組の色(青・緑・赤・不透明度の順のバイト)を、固めたブラシにする。</summary>
    private static SolidColorBrush[] ReadPalette(byte[] data, int cpal)
    {
        int entryCount = ReadUInt16(data, cpal + 2);
        var colorRecords = cpal + (int)ReadUInt32(data, cpal + 8);
        int firstIndex = ReadUInt16(data, cpal + 12);
        var brushes = new SolidColorBrush[entryCount];
        for (var index = 0; index < entryCount; index++)
        {
            var color = colorRecords + (firstIndex + index) * 4;
            var brush = new SolidColorBrush(Color.FromArgb(data[color + 3], data[color + 2], data[color + 1], data[color]));
            brush.Freeze();
            brushes[index] = brush;
        }

        return brushes;
    }

    private static ushort ReadUInt16(byte[] data, int offset) => (ushort)((data[offset] << 8) | data[offset + 1]);

    private static uint ReadUInt32(byte[] data, int offset) =>
        (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

    private sealed record ColorFont(
        GlyphTypeface GlyphTypeface,
        Dictionary<ushort, (ushort Glyph, ushort PaletteIndex)[]> Layers,
        SolidColorBrush[] PaletteBrushes);
}
