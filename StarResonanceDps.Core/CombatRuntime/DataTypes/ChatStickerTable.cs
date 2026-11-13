// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.Core.CombatRuntime.DataTypes
{
    public class ChatStickerTable
    {
        public Dictionary<string, ChatSticker> Data = [];
    }

    /// <summary>チャットの顔文字・スタンプの表の1行。種類は 1 が文に混ぜる顔文字、3・4 がスタンプ、5 が定型文(絵なし)。</summary>
    public class ChatSticker
    {
        public int Id { get; set; }
        public int Type { get; set; }

        /// <summary>絵の名前。定型文は空。</summary>
        public string Res { get; set; } = string.Empty;
    }
}
