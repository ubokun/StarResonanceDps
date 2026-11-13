// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

namespace StarResonanceDps.App.Services;

/// <summary>
/// VOICEVOX の話者のクレジット。基本は「VOICEVOX:話者名」。
/// 書式の違う話者は話者の ID(speaker_uuid)で直す(エンジンが返す話者の規約の表記。離途は本人の規約が求める「VOICEVOX 離途」)。
/// </summary>
public static class VoicevoxCredit
{
    private static readonly Dictionary<string, string> CreditsBySpeakerUuid = new(StringComparer.OrdinalIgnoreCase)
    {
        // もち子さん
        ["9f3ee141-26ad-437e-97bd-d22298d02ad2"] = "VOICEVOX:もち子(cv 明日葉よもぎ)",
        // Voidoll
        ["0ebe2c7d-96f3-4f0e-a2e3-ae13fe27c403"] = "VOICEVOX:Voidoll(CV:丹下桜)",
        // ユーレイちゃん
        ["462cd6b4-c088-42b0-b357-3816e24f112e"] = "VOICEVOX:ユーレイちゃん(CV:神崎零)",
        // 里石ユカ
        ["cc7baa71-6c74-4399-803b-b60576176a08"] = "VOICEVOX:里石ユカ（つぼみ）",
        // 離途
        ["3b91e034-e028-4acb-a08d-fbdcd207ea63"] = "VOICEVOX 離途"
    };

    public static string Get(string speakerUuid, string speakerName)
    {
        return CreditsBySpeakerUuid.TryGetValue(speakerUuid, out var credit)
            ? credit
            : "VOICEVOX:" + speakerName;
    }
}
