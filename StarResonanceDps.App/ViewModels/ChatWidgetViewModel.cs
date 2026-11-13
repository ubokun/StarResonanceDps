// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Threading;
using Serilog;
using StarResonanceDps.App.Behaviors;
using StarResonanceDps.App.Config;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Models.Widgets;
using StarResonanceDps.App.Services;
using StarResonanceDps.Core.CombatRuntime;
using StarResonanceDps.Core.Services;
using Zproto;

namespace StarResonanceDps.App.ViewModels;

/// <summary>
/// 画面に出すチャットの1件。発言者(伏せ字の設定を当てた名前とその色)・時刻(手元の時刻の「HH:mm」、通信の時刻が無ければ空)とその色、
/// 本文の並び(<see cref="Segments"/>)とその色。<see cref="Widget"/> は選んだ文字の地の色を窓のパレットから取るためのもの。
/// </summary>
public sealed record ChatEntry(
    string SenderName,
    Brush SenderNameBrush,
    string TimeText,
    Brush TimeBrush,
    IReadOnlyList<object> Segments,
    Brush MessageBrush,
    WidgetListItemViewModel Widget);

/// <summary>本文に混ぜる顔文字(本文の <c>&lt;sprite=N&gt;</c>)。<see cref="Emoji"/> は絵文字の文字で、書体の色の層で描く。</summary>
public sealed record ChatEmojiSegment(string Emoji);

/// <summary>スタンプの絵。<see cref="IconPath"/> は絵のファイルのパス(絵が無いスタンプは作らず、「[スタンプ]」の文字にする)。</summary>
public sealed record ChatStickerSegment(string IconPath);

/// <summary>
/// チャットの窓1枚。チャンネル1つ(ワールド・ギルド・パーティ・Ch・初心者)か、プライベートトークの相手1人のメッセージを、送られた時刻の順に出す。
///
/// <para>
/// 控え(<see cref="ChatMessageStore"/>)を <see cref="RefreshInterval"/> ごとに前回の続きから読み、増えた分だけ足す。
/// 一覧の版の番号が変わった(ログアウト・ゲームが一覧を読み直した・古い記録が前に足された)ら、持っている行を捨てて最初から読み直す。
/// 取得したメッセージは全部持ち、設定(テキストカラー・名前の伏せ字・言語)が変われば持っているメッセージから行を作り直す。
/// </para>
///
/// <para>
/// 本文はメッセージの種類で決める。スタンプは絵(絵が無ければ「[スタンプ]」、定型文は本文の文字)、画像・ボイスはゲームの表示名(ボイスは文字起こしが続く)、
/// リンクの発言は表の文の型を差し込みの値で埋めた文(<see cref="ChatHyperlinkFormatter"/>、解けなければ前後の文と「[リンク]」)、
/// ほかは本文の文字で、その中の <c>&lt;sprite=N&gt;</c> は顔文字(番号が表の外ならそのままの文字)、<c>http://</c> と <c>https://</c> の URL は押せるリンクにする。
/// スタンプは文字の入ったものに言語別の絵が、リンクの発言は言語ごとの文の型があるので、表示言語が変わったら行を作り直す。
/// </para>
///
/// <para>
/// 画面で選んだ文字は、右クリックの「コピー」で文字にする(<see cref="CreateCopyText"/>)。
/// </para>
/// </summary>
public sealed partial class ChatWidgetViewModel : ViewModelBase, IDisposable, IWidgetDataStatusSource, ITextRowsCopySource
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250);

    /// <summary>表に無いスタンプの番号。番号ごとに1回だけログに残す(画面のスレッドだけで使う)。</summary>
    private static readonly HashSet<uint> LoggedUnknownStickerIds = [];

    /// <summary>文を組めなかったリンクの発言の番号。番号ごとに1回だけログに残す(画面のスレッドだけで使う)。</summary>
    private static readonly HashSet<uint> LoggedUnresolvedHyperlinkIds = [];

    private readonly WidgetListItemViewModel _widget;
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<ChatMessageRecord> _records = [];
    private readonly ObservableCollection<ChatEntry> _entries = [];

    private ChatWidgetSettingsConfig _settings;
    private Brush _senderNameBrush;
    private Brush _timeBrush;
    private Brush _messageBrush;
    private int _revision = -1;
    private int _nextIndex;
    private string _headerText = string.Empty;

    /// <param name="partnerId">プライベートトークの相手の UID。ほかのチャンネルは 0。</param>
    /// <param name="savedPartnerName">前回開いていた窓が保存しておいた相手の名前。相手の名前が分かるまでタイトルに使う。</param>
    public ChatWidgetViewModel(
        WidgetListItemViewModel widget,
        ChitChatChannelType channel,
        long partnerId,
        string? savedPartnerName)
    {
        _widget = widget;
        Channel = channel;
        PartnerId = partnerId;
        LastKnownPartnerName = savedPartnerName;
        _settings = widget.GetChatSettingsSnapshot();
        _senderNameBrush = CreateTextBrush(WidgetConfigDefaults.ChatPlayerNameTextColorKey);
        _timeBrush = CreateTextBrush(WidgetConfigDefaults.ChatTimeTextColorKey);
        _messageBrush = CreateTextBrush(WidgetConfigDefaults.ChatMessageTextColorKey);
        Entries = new ReadOnlyObservableCollection<ChatEntry>(_entries);
        RefreshHeaderText();

        _refreshTimer = new DispatcherTimer
        {
            Interval = RefreshInterval
        };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _widget.ChatSettingsChanged += Widget_ChatSettingsChanged;
        _configManager.SettingsPreviewChanged += ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged += LocalizationManager_CultureChanged;

        Refresh();
        _refreshTimer.Start();
    }

    public ChitChatChannelType Channel { get; }

    /// <summary>プライベートトークの相手の UID。ほかのチャンネルは 0。</summary>
    public long PartnerId { get; }

    /// <summary>最後に分かっていた相手の名前(プライベートトーク)。窓を閉じても次の起動のタイトルに使えるよう保存する。</summary>
    public string? LastKnownPartnerName { get; private set; }

    public ReadOnlyObservableCollection<ChatEntry> Entries { get; }

    /// <summary>右クリックのメニューの色を窓のパレットから取るためのもの。</summary>
    public WidgetListItemViewModel Widget => _widget;

    public string HeaderText
    {
        get => _headerText;
        private set => SetProperty(ref _headerText, value);
    }

    public WidgetDataStatus DataStatus { get; private set; }

    public event EventHandler? DataStatusChanged;

    /// <summary>この窓が映す相手か。</summary>
    public bool Represents(ChitChatChannelType channel, long partnerId)
    {
        return Channel == channel && PartnerId == partnerId;
    }

    public void RefreshDataStatus()
    {
        var status = WidgetDataStatusRules.ForChat(
            GameReceptionStateStore.Current,
            ChatMessageStore.HasPastChat(new ChatTarget(Channel, PartnerId)));
        if (status == DataStatus)
        {
            return;
        }

        DataStatus = status;
        DataStatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= RefreshTimer_Tick;
        _widget.ChatSettingsChanged -= Widget_ChatSettingsChanged;
        _configManager.SettingsPreviewChanged -= ConfigManager_SettingsPreviewChanged;
        LocalizationManager.Instance.CultureChanged -= LocalizationManager_CultureChanged;
    }

    /// <summary>
    /// 選んだ文字のコピーの文字。メッセージの間は改行。範囲の行と位置の数え方は画面の選択(<see cref="TextRowsSelectionBehavior"/>)と同じで、
    /// 行の文字は <see cref="GetRowText"/>。
    /// </summary>
    public string CreateCopyText(TextRowsSelection selection)
    {
        var text = new StringBuilder();
        var lastRow = Math.Min(selection.EndRow, _entries.Count - 1);
        for (var row = Math.Max(selection.StartRow, 0); row <= lastRow; row++)
        {
            var rowText = GetRowText(_entries[row]);
            var start = row == selection.StartRow ? Math.Clamp(selection.StartOffset, 0, rowText.Length) : 0;
            var end = row == selection.EndRow ? Math.Clamp(selection.EndOffset, start, rowText.Length) : rowText.Length;
            if (row > selection.StartRow)
            {
                text.AppendLine();
            }

            text.Append(rowText[start..end].Replace("\n", Environment.NewLine));
        }

        return text.ToString();
    }

    /// <summary>
    /// 行の文字。名前と時刻(あいだは空白1つ)、改行して本文。本文は画面の文字(リンクは URL、顔文字は絵文字、スタンプは「[スタンプ]」)。
    /// 名前と時刻のあとの区切りは画面の選択の数え方と同じで、空でないときだけ1文字。
    /// </summary>
    private static string GetRowText(ChatEntry entry)
    {
        var text = new StringBuilder(entry.SenderName);
        if (entry.SenderName.Length > 0)
        {
            text.Append(entry.TimeText.Length > 0 ? ' ' : '\n');
        }

        if (entry.TimeText.Length > 0)
        {
            text.Append(entry.TimeText).Append('\n');
        }

        foreach (var segment in entry.Segments)
        {
            switch (segment)
            {
                case string part:
                    text.Append(part);
                    break;
                case TextLinkSegment link:
                    text.Append(link.Text);
                    break;
                case ChatEmojiSegment emoji:
                    text.Append(emoji.Emoji);
                    break;
                case ChatStickerSegment:
                    text.Append(LocalizationManager.Instance.GetString("Chat_Sticker"));
                    break;
            }
        }

        return text.ToString();
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        Refresh();
    }

    /// <summary>テキストカラー(保存かプレビュー)が変わった。持っているメッセージから行を作り直す。</summary>
    private void Widget_ChatSettingsChanged(object? sender, EventArgs e)
    {
        _settings = _widget.GetChatSettingsSnapshot();
        _senderNameBrush = CreateTextBrush(WidgetConfigDefaults.ChatPlayerNameTextColorKey);
        _timeBrush = CreateTextBrush(WidgetConfigDefaults.ChatTimeTextColorKey);
        _messageBrush = CreateTextBrush(WidgetConfigDefaults.ChatMessageTextColorKey);
        RebuildEntries();
    }

    /// <summary>名前の伏せ字は行の文字列に焼き込まれている。言語の切り替えと同じく、行とタイトルを作り直す。</summary>
    private void ConfigManager_SettingsPreviewChanged(object? sender, EventArgs e)
    {
        RefreshHeaderText();
        RebuildEntries();
    }

    private void LocalizationManager_CultureChanged(object? sender, EventArgs e)
    {
        RefreshHeaderText();
        RebuildEntries();
    }

    private void Refresh()
    {
        var slice = ChatMessageStore.GetMessages(new ChatTarget(Channel, PartnerId), _revision, _nextIndex);
        if (slice.Revision != _revision)
        {
            _revision = slice.Revision;
            _records.Clear();
            _entries.Clear();
        }

        _nextIndex = slice.NextIndex;
        foreach (var record in slice.Messages)
        {
            _records.Add(record);
            _entries.Add(CreateEntry(record));
        }

        // 名前は相手の発言だけでなく、後から届く名刺照会でも分かるので、メッセージが増えないときも引き直す。
        if (Channel == ChitChatChannelType.ChannelPrivate)
        {
            var partnerName = ChatMessageStore.GetPrivatePartnerName(PartnerId);
            if (partnerName.Length > 0 && !string.Equals(partnerName, LastKnownPartnerName, StringComparison.Ordinal))
            {
                LastKnownPartnerName = partnerName;
                RefreshHeaderText();
            }
        }

        RefreshDataStatus();
    }

    private void RefreshHeaderText()
    {
        HeaderText = WidgetHeaderText.ForChat(
            _widget.DisplayName,
            Channel,
            PartnerId,
            LastKnownPartnerName,
            PlayerRosterPresentationStore.Instance.NameDisplayMode);
    }

    private void RebuildEntries()
    {
        for (var index = 0; index < _records.Count; index++)
        {
            _entries[index] = CreateEntry(_records[index]);
        }
    }

    private ChatEntry CreateEntry(ChatMessageRecord record)
    {
        var timeText = record.SentAt is { } sentAt
            ? sentAt.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)
            : string.Empty;

        // 発言者のいないメッセージ(システムの通知)は、ゲームと同じく時刻だけを出す。
        var senderName = record.SenderId <= 0 && string.IsNullOrWhiteSpace(record.SenderName)
            ? string.Empty
            : PlayerInfoFormatFormatter.GetDisplayName(
                record.SenderName,
                record.SenderId,
                AppState.PlayerUID != 0 && record.SenderId == AppState.PlayerUID,
                false,
                0,
                PlayerRosterPresentationStore.Instance.NameDisplayMode);

        var segments = new List<object>();
        AddBodySegments(segments, record);
        return new ChatEntry(senderName, _senderNameBrush, timeText, _timeBrush, segments, _messageBrush, _widget);
    }

    private static void AddBodySegments(List<object> segments, ChatMessageRecord record)
    {
        var localization = LocalizationManager.Instance;
        switch (record.MessageType)
        {
            case ChitChatMsgType.ChatMsgPictureEmoji:
                AddStickerSegments(segments, record);
                break;
            case ChitChatMsgType.ChatMsgHypertext:
                AddHyperlinkSegments(segments, record);
                break;
            case ChitChatMsgType.ChatMsgPicture:
                segments.Add(localization.GetString("Chat_Image"));
                break;
            case ChitChatMsgType.ChatMsgVoice:
                segments.Add(string.IsNullOrEmpty(record.VoiceText)
                    ? localization.GetString("Chat_Voice")
                    : localization.Format("Chat_VoiceTextFormat", record.VoiceText));
                break;
            default:
                AddTextSegments(segments, record.Text);
                break;
        }
    }

    /// <summary>
    /// スタンプは番号(configId)を表で引いた絵。定型文(表の絵の名前が空)は本文に送った人の言語の文が入っているので、それを出す。
    /// 番号が表に無いときと絵のファイルが無いときは「[スタンプ]」の文字にする。
    /// </summary>
    private static void AddStickerSegments(List<object> segments, ChatMessageRecord record)
    {
        var resource = CombatDataCatalog.GetChatStickerResource(record.PictureEmojiConfigId);
        if (resource is null)
        {
            if (LoggedUnknownStickerIds.Add(record.PictureEmojiConfigId))
            {
                Log.Warning("Chat sticker {ConfigId} is not in ChatStickersTable; showing the sticker label", record.PictureEmojiConfigId);
            }

            segments.Add(LocalizationManager.Instance.GetString("Chat_Sticker"));
            return;
        }

        if (resource.Length == 0)
        {
            AddTextSegments(segments, record.Text);
            return;
        }

        var iconPath = CombatIconResolver.ResolveChatStickerIcon(resource, LocalizationManager.Instance.CurrentCulture.Name);
        segments.Add(iconPath is null
            ? LocalizationManager.Instance.GetString("Chat_Sticker")
            : new ChatStickerSegment(iconPath));
    }

    /// <summary>
    /// リンクの発言は、表の文の型を差し込みの値で埋めた文。文を組めなかったとき(表に番号が無い・型が空・値の取れない差し込みがある)は、
    /// 発言した人が打った前後の文のあいだに「[リンク]」の文字を出す。
    /// </summary>
    private static void AddHyperlinkSegments(List<object> segments, ChatMessageRecord record)
    {
        if (record.Hyperlink is not { } hyperlink)
        {
            if (LoggedUnresolvedHyperlinkIds.Add(0))
            {
                Log.Warning("Chat hyperlink message {MessageId} has no hyperlink data; showing the link label", record.MessageId);
            }

            segments.Add(LocalizationManager.Instance.GetString("Chat_Hyperlink"));
            return;
        }

        var text = ChatHyperlinkFormatter.Format(hyperlink, record.SenderName);
        if (text.Text is not null)
        {
            AddTextSegments(segments, text.Text);
            return;
        }

        if (LoggedUnresolvedHyperlinkIds.Add(hyperlink.ConfigId))
        {
            Log.Warning(
                "Chat hyperlink {ConfigId} could not be filled ({Problem}); showing the link label",
                hyperlink.ConfigId,
                text.Problem);
        }

        AddTextSegments(segments, text.Before);
        segments.Add(LocalizationManager.Instance.GetString("Chat_Hyperlink"));
        AddTextSegments(segments, text.After);
    }

    /// <summary>文の本文を、文字・顔文字(<c>&lt;sprite=N&gt;</c>)・リンク(URL)に分ける。</summary>
    private static void AddTextSegments(List<object> segments, string text)
    {
        var start = 0;
        foreach (Match match in GetBodyTokenRegex().Matches(text))
        {
            if (match.Index > start)
            {
                segments.Add(text[start..match.Index]);
            }

            var emojiNumber = match.Groups["emoji"];
            if (emojiNumber.Success)
            {
                var emoji = int.TryParse(emojiNumber.ValueSpan, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                    ? CombatDataCatalog.GetChatEmoji(number)
                    : null;
                segments.Add(emoji is null ? match.Value : new ChatEmojiSegment(emoji));
            }
            else if (Uri.TryCreate(match.Value, UriKind.Absolute, out var uri))
            {
                segments.Add(new TextLinkSegment(match.Value, uri));
            }
            else
            {
                segments.Add(match.Value);
            }

            start = match.Index + match.Length;
        }

        if (start < text.Length)
        {
            segments.Add(text[start..]);
        }
    }

    /// <summary>本文の顔文字(<c>&lt;sprite=N&gt;</c>)と URL。URL は URL に使える半角の文字が続く範囲で、日本語や空白の手前で終わる。</summary>
    [GeneratedRegex(@"<sprite=(?<emoji>\d+)>|https?://[A-Za-z0-9\-._~:/?#\[\]@!$&'()*+,;=%]+", RegexOptions.IgnoreCase)]
    private static partial Regex GetBodyTokenRegex();

    /// <summary>テキストカラーの設定で選んでいる色(引き方はタイムラインのテキストカラーと同じ)。</summary>
    private Brush CreateTextBrush(string colorKey)
    {
        var palette = _settings.TextColorPalettes.TryGetValue(colorKey, out var colors)
            ? colors
            : WidgetConfigDefaults.CreateDefaultChatTextColors(colorKey);
        var selectedIndex = _settings.TextColorIndexes.TryGetValue(colorKey, out var index)
            ? index
            : WidgetConfigDefaults.GetDefaultChatTextColorIndex(colorKey);
        var selectedColor = palette.Count == 0
            ? "#FFFFFF"
            : palette[Math.Clamp(selectedIndex, 0, palette.Count - 1)];

        var brush = new SolidColorBrush(ColorUtilities.TryParseHex(selectedColor, out var color) ? color : Colors.White);
        brush.Freeze();
        return brush;
    }
}
