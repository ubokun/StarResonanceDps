// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Text;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.Core.Logging;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// ログタブの行。1件の記録を1行にする(文字は <see cref="PacketDiagnosticLogEntry.DisplayText"/>)。
///
/// <para>
/// 控え(<see cref="PacketDiagnosticLogStore"/>)は上限を超えると前から外すので、読み直しでは前から外れた分を外し、後ろに増えた分を足す。
/// 行を作り直さないので、選んだ文字は外れた行の分だけ番号がずれて残る(<see cref="TextRowsSelectionBehavior"/>)。
/// 画面で選んだ文字は、右クリックの「コピー」で文字にする(<see cref="CreateCopyText"/>)。
/// </para>
/// </summary>
public sealed class LogsViewModel : ITextRowsCopySource
{
    public ObservableCollection<PacketDiagnosticLogEntry> Lines { get; } = [];

    /// <summary>
    /// 控えの今の中身に合わせる。記録は参照で突き合わせる(同じ文の記録が続いても別の行)。
    /// 今の先頭の行が控えに無い・並びが合わないときは作り直す。
    /// </summary>
    public void Refresh(IReadOnlyList<PacketDiagnosticLogEntry> snapshot)
    {
        if (snapshot.Count == 0)
        {
            Lines.Clear();
            return;
        }

        var removedCount = IndexOfLine(snapshot[0]);
        if (removedCount < 0 || !MatchesFrom(removedCount, snapshot))
        {
            Rebuild(snapshot);
            return;
        }

        for (var index = 0; index < removedCount; index++)
        {
            Lines.RemoveAt(0);
        }

        for (var index = Lines.Count; index < snapshot.Count; index++)
        {
            Lines.Add(snapshot[index]);
        }
    }

    public string CreateCopyText(TextRowsSelection selection)
    {
        var text = new StringBuilder();
        var lastRow = Math.Min(selection.EndRow, Lines.Count - 1);
        for (var row = Math.Max(selection.StartRow, 0); row <= lastRow; row++)
        {
            var rowText = Lines[row].DisplayText;
            var start = row == selection.StartRow ? Math.Clamp(selection.StartOffset, 0, rowText.Length) : 0;
            var end = row == selection.EndRow ? Math.Clamp(selection.EndOffset, start, rowText.Length) : rowText.Length;
            if (row > selection.StartRow)
            {
                text.AppendLine();
            }

            text.Append(rowText, start, end - start);
        }

        return text.ToString();
    }

    /// <summary>行が空なら 0(全部を後ろに足す)。見つからなければ -1。</summary>
    private int IndexOfLine(PacketDiagnosticLogEntry entry)
    {
        if (Lines.Count == 0)
        {
            return 0;
        }

        for (var index = 0; index < Lines.Count; index++)
        {
            if (ReferenceEquals(Lines[index], entry))
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary><paramref name="start"/> 行目から後ろの行が、控えの先頭から同じ記録で並んでいるか。</summary>
    private bool MatchesFrom(int start, IReadOnlyList<PacketDiagnosticLogEntry> snapshot)
    {
        var count = Lines.Count - start;
        if (count > snapshot.Count)
        {
            return false;
        }

        for (var index = 0; index < count; index++)
        {
            if (!ReferenceEquals(Lines[start + index], snapshot[index]))
            {
                return false;
            }
        }

        return true;
    }

    private void Rebuild(IReadOnlyList<PacketDiagnosticLogEntry> snapshot)
    {
        Lines.Clear();
        foreach (var entry in snapshot)
        {
            Lines.Add(entry);
        }
    }
}
