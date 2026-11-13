// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 行を掴んで並べ替える一覧の ViewModel(一覧の <c>DataContext</c>)が実装する取り決め。
/// 掴む・写す・薄くする・予定の位置を決める・窓の外の扱いは画面の振る舞い(<c>Behaviors.RowReorderBehavior</c>)が受け持ち、
/// ここでは並びの持ち方と確定だけを決める。
/// </summary>
public interface IRowReorderTarget
{
    /// <summary>
    /// 行(一覧の項目)の並べ替えの単位の鍵。同じ鍵の行(組)は一覧の中で並んでいて、一緒に掴まれて動く。
    /// null の行は掴めない。鍵は <see cref="object.Equals(object)"/> で比べる。
    /// </summary>
    object? GetRowReorderKey(object item);

    /// <summary>掴み始め。並びはまだ変えない。</summary>
    void BeginRowReorder(object key);

    /// <summary>
    /// 掴んでいる間の見た目だけの並び。掴んだ単位を <paramref name="beforeKey"/> の単位の前(null なら末尾)へ動かす。
    /// 確定は <see cref="EndRowReorder"/>。
    /// </summary>
    void ShowRowReorderAt(object? beforeKey);

    /// <summary>掴んでいる間に、掴んだ単位を元の位置へ戻す(窓の外にいる間)。</summary>
    void ShowRowReorderAtOriginalPosition();

    /// <summary>掴み終わり。<paramref name="commit"/> なら今の見た目の並びを確定し、そうでなければ元の並びへ戻す。</summary>
    void EndRowReorder(bool commit);
}
