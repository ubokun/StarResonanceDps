// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime;

/// <summary>
/// シーンの地図1枚。<c>Data/Generated/Maps.json</c> の <c>Maps</c> の1項目で、<see cref="CombatDataCatalog.GetSceneMap"/> が返す。
/// ワールド座標(水平は x と z)から画像の位置へは、横の割合 = (x − <see cref="X0"/>) ÷ <see cref="Width"/>、
/// 縦の割合 = 1 − (z − <see cref="Z0"/>) ÷ <see cref="Depth"/>(画像の上が z の大きい側)。
/// </summary>
/// <param name="Key">地図のキー。同じ地図を使うシーンは同じキーを持つ。</param>
/// <param name="X0">地図の左端のワールド座標の x。</param>
/// <param name="Z0">地図の下端のワールド座標の z。</param>
/// <param name="Width">ワールド座標の x の幅。</param>
/// <param name="Depth">ワールド座標の z の幅。</param>
/// <param name="ImageName">基本の画像(階層 0)のファイルの名前(拡張子なし)。</param>
/// <param name="LayerImageNames">階層 → その階層の画像のファイルの名前。階層 0 は持たず、階層の画像が無い地図は空。</param>
public sealed record SceneMapDefinition(
    string Key,
    double X0,
    double Z0,
    double Width,
    double Depth,
    string ImageName,
    IReadOnlyDictionary<int, string> LayerImageNames)
{
    /// <summary>その階層の画像のファイルの名前。その階層の画像が無ければ基本の画像。</summary>
    public string GetImageName(int layer)
    {
        return layer != 0 && LayerImageNames.TryGetValue(layer, out var name) ? name : ImageName;
    }
}
