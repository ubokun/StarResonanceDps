namespace StarResonanceDps.App.Services;

/// <summary>
/// プラグインが定義で渡す ID(ウィジェット・通知の項目・設定・選択肢)の決まり。SDK の各定義の説明と同じ。
/// </summary>
internal static class PluginIdRules
{
    private const int MaxLength = 128;

    /// <summary>ID が、1〜<see cref="MaxLength"/> 文字の ASCII の英数字と「.」「-」「_」だけか。前後の空白も認めない(プラグインは書いたままの ID で引く)。</summary>
    public static bool IsValid(string? id)
    {
        var value = id ?? string.Empty;
        if (value.Length == 0 || value.Length > MaxLength)
        {
            return false;
        }

        foreach (var character in value)
        {
            var isAllowed = character is >= 'a' and <= 'z'
                or >= 'A' and <= 'Z'
                or >= '0' and <= '9'
                or '.' or '-' or '_';
            if (!isAllowed)
            {
                return false;
            }
        }

        return true;
    }
}
