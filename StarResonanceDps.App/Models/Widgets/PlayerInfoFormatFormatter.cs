using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Models;
using StarResonanceDps.Core.Services;

namespace StarResonanceDps.App.Models.Widgets;

public static partial class PlayerInfoFormatFormatter
{
    private const string HiddenPlayerName = "*****";

    /// <summary>設定画面の見本に出すシーズンタレントの型(根ノードのバフ 3002810、4言語とも名前が短い型)。</summary>
    private const int PreviewSeasonTalentBuffId = 3002810;

    private const string NamePlaceholder = "{PlayerName}";
    private const string SpecPlaceholder = "{Spec}";
    private const string PsychPlaceholder = "{Psych}";
    private const string PowerLevelPlaceholder = "{PowerLevel}";
    private const string SeasonStrengthPlaceholder = "{SeasonStrength}";
    private const string SeasonLevelPlaceholder = "{SeasonLevel}";
    private const string UidPlaceholder = "{Uid}";

    /// <summary>書式に差し込める項目(設定の「プレイヤー名」の項目の一覧)。プレイヤーリスト・メーター・蘇生リストで同じ。</summary>
    public static IReadOnlyList<(string Key, string LabelResourceKey, string Placeholder)> FieldDefinitions { get; } =
    [
        ("PlayerName", "Settings_PlayerInfo_Field_Name", NamePlaceholder),
        ("Spec", "Settings_PlayerInfo_Field_Class", SpecPlaceholder),
        ("Psych", "Settings_PlayerInfo_Field_SeasonTalent", PsychPlaceholder),
        ("PowerLevel", "Settings_PlayerInfo_Field_AbilityScore", PowerLevelPlaceholder),
        ("SeasonStrength", "Settings_PlayerInfo_Field_SeasonStrength", SeasonStrengthPlaceholder),
        ("SeasonLevel", "Settings_PlayerInfo_Field_SeasonLevel", SeasonLevelPlaceholder),
        ("Uid", "Settings_PlayerInfo_Field_PlayerUid", UidPlaceholder)
    ];

    /// <param name="cache">行ごとの控え。定期更新や名簿の更新で同じ行から何度も呼ぶ所が渡す。</param>
    public static string Format(
        MeterPlayerSnapshot player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode,
        PlayerInfoFormatCache? cache = null)
    {
        // 特化は Core で解決済みのものをそのまま使う。ここで SubProfessionId から
        // 組み立て直すと Rank1(アビリティ未装着)を表現できず、メーターだけ「不明」になる。
        return Format(
            new PlayerInfoFormatData(
                player.UserId,
                player.Name,
                player.ProfessionId,
                player.ClassSpec,
                player.AbilityScore,
                player.SeasonStrength,
                player.SeasonLevel,
                player.IsSelf,
                player.IsNpc,
                player.SeasonTalentBuffId,
                player.IsSeasonTalentInactive),
            formatString,
            nameDisplayMode,
            cache);
    }

    /// <param name="cache">行ごとの控え。定期更新や名簿の更新で同じ行から何度も呼ぶ所が渡す。</param>
    public static string Format(
        PlayerRosterEntry player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode,
        PlayerInfoFormatCache? cache = null)
    {
        return Format(
            new PlayerInfoFormatData(
                player.CharacterId,
                player.Name,
                player.ProfessionId,
                player.ClassSpec,
                player.CombatPower,
                player.SeasonStrength,
                player.SeasonLevel,
                player.IsSelf,
                player.IsNpc,
                player.SeasonTalentBuffId,
                player.IsSeasonTalentInactive),
            formatString,
            nameDisplayMode,
            cache);
    }

    public static string FormatPreview(string? formatString)
    {
        return Format(
            new PlayerInfoFormatData(
                39733357,
                LocalizationManager.Instance.GetString("Settings_PlayerInfo_PreviewName"),
                2,
                PlayerClassSpec.FrostMageIcicle,
                25000,
                1800,
                50,
                true,
                false,
                PreviewSeasonTalentBuffId,
                false),
            formatString,
            PlayerNameDisplayMode.Show,
            null);
    }

    private static string Format(
        PlayerInfoFormatData player,
        string? formatString,
        PlayerNameDisplayMode nameDisplayMode,
        PlayerInfoFormatCache? cache)
    {
        if (string.IsNullOrEmpty(formatString))
        {
            return string.Empty;
        }

        // 控えの比べは差し込む文字で行う。言語や内部IDの表示を変えると、ここで引く文字が変わる。
        var input = new PlayerInfoFormatInput(
            formatString,
            GetName(player, nameDisplayMode),
            GetClassSpecText(player.ClassSpec),
            GetSeasonTalentText(player.SeasonTalentBuffId, player.IsSeasonTalentInactive),
            player.AbilityScore,
            player.SeasonStrength,
            player.SeasonLevel,
            player.UserId);
        if (cache is not null && cache.TryGet(input, out var cached))
        {
            return cached;
        }

        var formatted = Build(input);
        cache?.Set(input, formatted);
        return formatted;
    }

    private static string Build(in PlayerInfoFormatInput input)
    {
        var result = input.FormatString
            .Replace(NamePlaceholder, input.Name, StringComparison.OrdinalIgnoreCase)
            .Replace(SpecPlaceholder, input.Spec, StringComparison.OrdinalIgnoreCase)
            .Replace(PsychPlaceholder, input.SeasonTalent, StringComparison.OrdinalIgnoreCase)
            .Replace(PowerLevelPlaceholder, input.AbilityScore.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace(SeasonStrengthPlaceholder, input.SeasonStrength.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace(SeasonLevelPlaceholder, input.SeasonLevel.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace(UidPlaceholder, input.UserId.ToString(), StringComparison.OrdinalIgnoreCase);

        result = GetCollapseWhitespaceRegex().Replace(result, " ");
        result = GetEmptyParenthesisRegex().Replace(result, string.Empty);
        result = GetEmptyBracketRegex().Replace(result, string.Empty);
        result = GetRepeatedHyphensRegex().Replace(result, " - ");
        result = GetLeadingOrTrailingHyphenRegex().Replace(result, string.Empty);

        return result.Trim();
    }

    /// <summary>
    /// NPC の規則を当てる前の名前の表示値。伏せる設定なら伏せ字、
    /// <b>名前がまだ取れていなければUID</b>、それ以外は名前そのもの。
    ///
    /// <para>
    /// <b>外へ出さない。</b>表示はどこも NPC の印を渡す版を通す。
    /// NPC の印を渡さずに名前を出せる入口があると、そこだけ NPC の規則が抜ける。
    /// </para>
    /// </summary>
    private static string GetDisplayName(
        string? name,
        long userId,
        bool isSelf,
        PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(isSelf, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        return string.IsNullOrWhiteSpace(name)
            ? LocalizationManager.Instance.Format("PlayerName_UidFormat", userId)
            : name;
    }

    /// <summary>
    /// 相手の名前の表示値。<b>NPC は名前ではなく職業名を出す</b>(プレイヤー一覧と同じ規則)。
    /// 書式を通さない表示(ウィンドウのタイトル、バフ・デバフカードの本文)もこれを通す。
    /// </summary>
    public static string GetDisplayName(
        string? name,
        long userId,
        bool isSelf,
        bool isNpc,
        int professionId,
        PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(isSelf, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        return isNpc
            ? LocalizationManager.Instance.GetString($"Classes_{PlayerProfession.GetKey(professionId)}")
            : GetDisplayName(name, userId, isSelf, nameDisplayMode);
    }

    /// <summary>
    /// タイムライン・被ダメログの登場人物(加害者・対象)と、バフ/デバフリストの付与者(エンティティのリストでプレイヤーが付けた行)の表示値。
    /// システム(召喚者へ寄せられなかった仮想体)は「システム」、名前の無い HP バーの見える敵は「敵」、何者か分からない敵は「未知の敵」、
    /// プレイヤーはプレイヤー名の規則(<see cref="GetDisplayName(string?, long, bool, bool, int, PlayerNameDisplayMode)"/>)、
    /// それ以外は名前のまま。
    /// </summary>
    public static string GetPartyDisplayName(CombatEventLogParty person)
    {
        if (person.IsSystem)
        {
            return LocalizationManager.Instance["CombatEventLog_SystemActor"];
        }

        // 名前の無い、HP バーの見える敵。文言はエンティティリストと同じ。名前には内部ID注記だけが入っていることがある。
        if (person.IsUnnamedEnemy)
        {
            return LocalizationManager.Instance["EntityList_UnnamedEnemy"] + person.Name;
        }

        // 何者か分からない敵。名前の無いモンスターなら、名前には内部ID注記だけが入っていることがある。
        if (person.IsUnknownEnemy)
        {
            return LocalizationManager.Instance["EntityList_UnknownEnemy"] + person.Name;
        }

        return person.IsPlayer
            ? GetDisplayName(
                person.Name,
                person.CharacterId,
                person.IsSelf,
                person.IsNpc,
                person.ProfessionId,
                PlayerRosterPresentationStore.Instance.NameDisplayMode)
            : person.Name;
    }

    private static string GetName(PlayerInfoFormatData player, PlayerNameDisplayMode nameDisplayMode)
    {
        if (ShouldHideName(player.IsSelf, nameDisplayMode))
        {
            return HiddenPlayerName;
        }

        return GetDisplayName(
            player.Name,
            player.UserId,
            player.IsSelf,
            player.IsNpc,
            player.ProfessionId,
            nameDisplayMode);
    }

    /// <summary>
    /// シーズンタレントの型の表示値。型が分かれば型の名前、無効なら「無効」、どちらでもなければ「不明」。
    /// 特化の <c>{Spec}</c> と同じく、不明も空にせず文字で出す。書式の <c>{Psych}</c> とプレイヤーリストのツールチップが使う。
    /// </summary>
    public static string GetSeasonTalentText(int seasonTalentBuffId, bool isSeasonTalentInactive)
    {
        if (seasonTalentBuffId > 0)
        {
            return CombatDataCatalog.GetSeasonTalentName(seasonTalentBuffId);
        }

        return LocalizationManager.Instance.GetString(
            IsSeasonTalentUnknown(seasonTalentBuffId, isSeasonTalentInactive) ? "SeasonTalent_Unknown" : "SeasonTalent_Inactive");
    }

    /// <summary>潜在心相晶の型がまだ分からない(型が無く、無効とも確定していない)。表示の「不明」とウィジェットの状態の印が使う。</summary>
    public static bool IsSeasonTalentUnknown(int seasonTalentBuffId, bool isSeasonTalentInactive)
        => seasonTalentBuffId <= 0 && !isSeasonTalentInactive;

    /// <summary>書式が潜在心相晶(<c>{Psych}</c>)を出すか。ウィジェットの状態の印が、型の「不明」を数えるかに使う。</summary>
    public static bool ShowsSeasonTalent(string? format)
        => !string.IsNullOrEmpty(format) && format.Contains(PsychPlaceholder, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 特化の表示値。特化(クラスR2)なら名前の表 <c>ClassSpecNames.json</c> の名前、
    /// それ以外(クラスR1・変身・不明)はリソース <c>ClassSpec_&lt;特化&gt;</c> の文字。
    /// 書式の <c>{Spec}</c>、プレイヤーリスト、タイムライン、プレイヤー情報が使う。
    /// </summary>
    public static string GetClassSpecText(PlayerClassSpec classSpec)
    {
        var subProfessionId = PlayerClassSpecResolver.ToSubProfessionId(classSpec);
        return subProfessionId > 0
            ? CombatDataCatalog.GetClassSpecName(subProfessionId)
            : LocalizationManager.Instance.GetString($"ClassSpec_{classSpec}");
    }

    internal static bool ShouldHideName(bool isSelf, PlayerNameDisplayMode nameDisplayMode)
    {
        return nameDisplayMode switch
        {
            PlayerNameDisplayMode.Hide => true,
            PlayerNameDisplayMode.HideOthers => !isSelf,
            _ => false
        };
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex GetCollapseWhitespaceRegex();

    [GeneratedRegex(@"\(\s*\)")]
    private static partial Regex GetEmptyParenthesisRegex();

    [GeneratedRegex(@"\[\s*\]")]
    private static partial Regex GetEmptyBracketRegex();

    [GeneratedRegex(@"\s*-\s*-\s*")]
    private static partial Regex GetRepeatedHyphensRegex();

    [GeneratedRegex(@"^\s*-\s*|\s*-\s*$")]
    private static partial Regex GetLeadingOrTrailingHyphenRegex();

    private readonly record struct PlayerInfoFormatData(
        long UserId,
        string Name,
        int ProfessionId,
        PlayerClassSpec ClassSpec,
        int AbilityScore,
        int SeasonStrength,
        int SeasonLevel,
        bool IsSelf,
        bool IsNpc,
        int SeasonTalentBuffId,
        bool IsSeasonTalentInactive);
}

/// <summary>書式に差し込む値。同じなら結果の文字列も同じ。</summary>
internal readonly record struct PlayerInfoFormatInput(
    string FormatString,
    string Name,
    string Spec,
    string SeasonTalent,
    int AbilityScore,
    int SeasonStrength,
    int SeasonLevel,
    long UserId);

/// <summary>行ごとに持つ、書式の前回の入力と結果。入力が同じなら前の文字列を返す。行と一緒に解放される。</summary>
public sealed class PlayerInfoFormatCache
{
    private PlayerInfoFormatInput _input;
    private string? _result;

    internal bool TryGet(in PlayerInfoFormatInput input, [NotNullWhen(true)] out string? result)
    {
        result = _result;
        return result is not null && _input == input;
    }

    internal void Set(in PlayerInfoFormatInput input, string result)
    {
        _input = input;
        _result = result;
    }
}
