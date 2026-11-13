// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Globalization;
using System.Text.RegularExpressions;
using StarResonanceDps.App.Localization;
using StarResonanceDps.Core.CombatRuntime;
using Zproto;

namespace StarResonanceDps.App.Models.Widgets;

/// <summary>
/// リンクの発言の文の組み立ての結果(<see cref="ChatHyperlinkFormatter.Format"/>)。
/// </summary>
/// <param name="Text">型を埋めた文。解けなかったときは null。</param>
/// <param name="Before">リンクより前に発言した人が打った文。</param>
/// <param name="After">リンクより後に発言した人が打った文。</param>
/// <param name="Problem">解けなかった理由(ログに出す英語の文)。解けたときは null。</param>
public readonly record struct ChatHyperlinkText(string? Text, string Before, string After, string? Problem);

/// <summary>
/// リンクの発言の文。ゲームのチャットの窓と同じく、表 <c>ChatHyperlink</c> の文の型を差し込みの値で埋め、
/// 書式のタグ(下線・リンク・文字の色)は外す。リンクの前後の文は、顔文字の <c>&lt;sprite=N&gt;</c> 以外のタグを外す。
///
/// <para>
/// 差し込みの名前と値の出所:
/// 文字の値は、リンクの値より前が <c>string[1]</c>・後が <c>string[2]</c>。
/// プレイヤーは1人なら <c>player.name</c> / <c>player.names</c>、2人以上なら <c>player.names[n]</c>。マスターの証は名前を <c>player.names</c>。
/// 魚・魚ランキング・釣りの総計は <c>fishing.playerName</c> が発言者の名前、<c>fishing.fishName</c> が魚のアイテム名、<c>fishing.rank</c> が順位。
/// 場所は <c>scence.name</c> がシーン名、<c>val1</c>〜<c>val5</c> がチャンネル・X・Y・Z・階層。
/// 数値・アイテム・バフは1つなら <c>val</c> / <c>item.name</c> / <c>buff.name</c>、2つ以上なら <c>arrVal[n]</c> / <c>item.names[n]</c> / <c>buff.names[n]</c>。
/// ギルドの建物は <c>union.build</c>、時刻は <c>timestamp</c>(表示中の言語の日時の書式で、PC の時刻帯)。
/// </para>
///
/// <para>
/// 表に番号が無い・型が空・値の取れない差し込みがある(解けない種類・名前の無いアイテムなど)ときは解けない。
/// 呼び出し側は前後の文とリンクの文字を出す。
/// </para>
/// </summary>
public static partial class ChatHyperlinkFormatter
{
    public static ChatHyperlinkText Format(ChatHyperlinkRecord hyperlink, string senderName)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var before = string.Empty;
        var after = string.Empty;
        var hasLinkValue = false;
        var players = new List<string>();
        var numbers = new List<string>();
        var items = new List<string>();
        var buffs = new List<string>();

        foreach (var value in hyperlink.Values)
        {
            if (value.Type == PlaceHolderType.String)
            {
                var text = value.Value is PlaceHolderStr str ? RemoveTagsOtherThanEmojis(str.Text) : string.Empty;
                if (hasLinkValue)
                {
                    after = text;
                }
                else
                {
                    before = text;
                }

                continue;
            }

            hasLinkValue = true;
            switch (value.Value)
            {
                case PlaceHolderPlayer player:
                    players.Add(player.Name);
                    break;
                case PlaceHolderMasterMode masterMode:
                    SetName(values, "player.names", masterMode.UserName);
                    break;
                case PlaceHolderFishItem fish:
                    SetName(values, "fishing.playerName", senderName);
                    SetName(values, "fishing.fishName", CombatDataCatalog.GetItemName(fish.FishId));
                    break;
                case PlaceHolderFishRank fishRank:
                    SetName(values, "fishing.playerName", senderName);
                    SetName(values, "fishing.fishName", CombatDataCatalog.GetItemName(fishRank.FishId));
                    values["fishing.rank"] = fishRank.Rank.ToString(CultureInfo.InvariantCulture);
                    break;
                case PlaceHolderFishPersonalTotal:
                    SetName(values, "fishing.playerName", senderName);
                    break;
                case PlaceHolderScenePosition position:
                    SetName(values, "scence.name", CombatDataCatalog.GetSceneNameWithoutInternalId(position.SceneId, 0));
                    values["val1"] = position.LineId.ToString(CultureInfo.InvariantCulture);
                    values["val2"] = position.PositionX.ToString(CultureInfo.InvariantCulture);
                    values["val3"] = position.PositionY.ToString(CultureInfo.InvariantCulture);
                    values["val4"] = position.PositionZ.ToString(CultureInfo.InvariantCulture);
                    values["val5"] = position.MapLayerId.ToString(CultureInfo.InvariantCulture);
                    break;
                case PlaceHolderVal number:
                    numbers.Add(number.Value.ToString(CultureInfo.InvariantCulture));
                    break;
                case PlaceHolderItem item:
                    items.Add(CombatDataCatalog.GetItemName((int)item.ConfigId));
                    break;
                case PlaceHolderUnion union:
                    SetName(values, "union.build", CombatDataCatalog.GetUnionBuildingName((int)union.Build));
                    break;
                case PlaceHolderBuff buff:
                    buffs.Add(CombatDataCatalog.GetBuffNameWithoutInternalId(buff.BuffId));
                    break;
                case PlaceHolderTimestamp timestamp:
                    values["timestamp"] = DateTimeOffset.FromUnixTimeSeconds(timestamp.Timestamp)
                        .ToLocalTime()
                        .ToString("G", LocalizationManager.Instance.CurrentCulture);
                    break;
            }
        }

        values["string[1]"] = before;
        values["string[2]"] = after;
        SetList(values, "player", players, singleAlsoAsNames: true);
        SetList(values, "item", items, singleAlsoAsNames: false);
        SetList(values, "buff", buffs, singleAlsoAsNames: false);
        if (numbers.Count == 1)
        {
            values["val"] = numbers[0];
        }
        else
        {
            for (var index = 0; index < numbers.Count; index++)
            {
                values[$"arrVal[{index + 1}]"] = numbers[index];
            }
        }

        var template = CombatDataCatalog.GetChatHyperlinkTemplate(hyperlink.ConfigId);
        if (template.Length == 0)
        {
            return new ChatHyperlinkText(null, before, after, "no template");
        }

        string? missingKey = null;
        var result = GetValueKeyRegex().Replace(GetTagRegex().Replace(template, string.Empty), match =>
        {
            var key = match.Groups["key"].Value;
            if (values.TryGetValue(key, out var text))
            {
                return text;
            }

            missingKey ??= key;
            return match.Value;
        });

        return missingKey is null
            ? new ChatHyperlinkText(result, before, after, null)
            : new ChatHyperlinkText(null, before, after, $"no value for {missingKey}");
    }

    /// <summary>名前の差し込み。空の名前は値として入れない(その差し込みは値の取れないものになる)。</summary>
    private static void SetName(Dictionary<string, string> values, string key, string name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            values[key] = name;
        }
    }

    /// <summary>
    /// 同じ種類の値の差し込み。1つなら <c>{prefix}.name</c>(<paramref name="singleAlsoAsNames"/> なら <c>{prefix}.names</c> にも)、
    /// 2つ以上なら <c>{prefix}.names[n]</c>。
    /// </summary>
    private static void SetList(Dictionary<string, string> values, string prefix, List<string> names, bool singleAlsoAsNames)
    {
        if (names.Count == 1)
        {
            SetName(values, $"{prefix}.name", names[0]);
            if (singleAlsoAsNames)
            {
                SetName(values, $"{prefix}.names", names[0]);
            }

            return;
        }

        for (var index = 0; index < names.Count; index++)
        {
            SetName(values, $"{prefix}.names[{index + 1}]", names[index]);
        }
    }

    private static string RemoveTagsOtherThanEmojis(string text)
    {
        return GetNonEmojiTagRegex().Replace(text, string.Empty);
    }

    /// <summary>差し込みの値 <c>{*名前*}</c>。</summary>
    [GeneratedRegex(@"\{\*(?<key>[^{}*]+)\*\}")]
    private static partial Regex GetValueKeyRegex();

    /// <summary>型の書式のタグ(<c>&lt;u&gt;</c>・<c>&lt;link=1&gt;</c>・<c>&lt;style=…&gt;</c> とその閉じ)。</summary>
    [GeneratedRegex(@"<[^<>]*>")]
    private static partial Regex GetTagRegex();

    /// <summary>顔文字の <c>&lt;sprite=N&gt;</c> 以外のタグ。</summary>
    [GeneratedRegex(@"<(?!sprite=\d+>)[^<>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex GetNonEmojiTagRegex();
}
