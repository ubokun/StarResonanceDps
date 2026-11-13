// This file is part of StarResonanceDps. See LICENSING.md for the license, additional permissions and additional terms.

using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Serilog;
using StarResonanceDps.App.Localization;
using StarResonanceDps.App.Services;
using StarResonanceDps.App.ViewModels;

namespace StarResonanceDps.App.Views.Widgets;

public partial class PlayerInfoWidgetView : UserControl
{
    private const string ImageContextMenuStyleKey = "Menu.WidgetWindowPlayerInfoImageContextMenu";
    private const string ImageContextMenuFirstItemStyleKey = "Menu.WidgetWindowPlayerInfoImageContextMenuItem.First";
    private const string ImageContextMenuMiddleItemStyleKey = "Menu.WidgetWindowPlayerInfoImageContextMenuItem.Middle";
    private const string ImageContextMenuLastItemStyleKey = "Menu.WidgetWindowPlayerInfoImageContextMenuItem.Last";

    /// <summary>メニューを開いたときの写真。項目を押したときはこれを保存・コピーする(開いている間に写真が替わっても、右クリックした絵を使う)。</summary>
    private PlayerPhoto? _menuPhoto;

    public PlayerInfoWidgetView()
    {
        InitializeComponent();
    }

    private void AvatarImage_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenImageMenu(sender, (DataContext as PlayerInfoWidgetViewModel)?.SavableAvatarPhoto, e);
    }

    private void CardImage_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        OpenImageMenu(sender, (DataContext as PlayerInfoWidgetViewModel)?.SavableCardPhoto, e);
    }

    /// <summary>自分の写真を出しているときだけ開く。既定の絵・取得中・他人の写真では何もしない。</summary>
    private void OpenImageMenu(object sender, PlayerPhoto? photo, MouseButtonEventArgs e)
    {
        if (photo is null || sender is not Image { ContextMenu: ContextMenu menu } image)
        {
            return;
        }

        _menuPhoto = photo;
        ApplyImageMenuStyles(menu);
        menu.PlacementTarget = image;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ApplyImageMenuStyles(ContextMenu menu)
    {
        if (TryFindResource(ImageContextMenuStyleKey) is Style contextMenuStyle)
        {
            menu.Style = contextMenuStyle;
        }

        var menuItems = menu.Items.OfType<MenuItem>()
            .Where(item => item.Visibility == Visibility.Visible)
            .ToArray();
        for (var index = 0; index < menuItems.Length; index++)
        {
            var styleKey = index switch
            {
                0 => ImageContextMenuFirstItemStyleKey,
                var lastIndex when lastIndex == menuItems.Length - 1 => ImageContextMenuLastItemStyleKey,
                _ => ImageContextMenuMiddleItemStyleKey
            };

            if (TryFindResource(styleKey) is Style menuItemStyle)
            {
                menuItems[index].Style = menuItemStyle;
            }
        }
    }

    // 前に使ったフォルダと窓の大きさの記録を、ほかのファイル・フォルダの窓と分ける番号。
    private static readonly Guid SaveImageDialogClientGuid = new("70D85BC1-61E7-4628-9ED6-6F2649BD7D4A");

    /// <summary>
    /// 届いたままの中身をそのまま書く。既定のファイル名はリンクの最後の部分、種類はリンクの拡張子とすべてのファイルの2択。
    /// オーナーはこのウィジェットの窓(ピン留め中はクリックしてもアクティブにならないので、渡さないと別の窓がオーナーになる)。
    /// </summary>
    private void SaveImageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_menuPhoto is not { } photo)
        {
            return;
        }

        var localization = LocalizationManager.Instance;
        var fileName = GetLinkFileName(photo.Url);
        var extension = Path.GetExtension(fileName);
        var allFilesFilter = $"{localization.GetString("PlayerInfo_SaveImageAllFilesFilter")}|*.*";
        var filter = string.IsNullOrEmpty(extension)
            ? allFilesFilter
            : $"{string.Format(localization.GetString("PlayerInfo_SaveImageFilterFormat"), extension.TrimStart('.').ToUpperInvariant())}|*{extension}|{allFilesFilter}";

        var dialog = new SaveFileDialog
        {
            ClientGuid = SaveImageDialogClientGuid,
            Title = localization.GetString("PlayerInfo_SaveImageDialogTitle"),
            FileName = fileName,
            Filter = filter
        };

        if (UserFolders.Pictures is { } pictures)
        {
            dialog.DefaultDirectory = pictures;
        }

        var owner = Window.GetWindow(this);
        if (dialog.ShowDialog(owner) != true)
        {
            return;
        }

        try
        {
            File.WriteAllBytes(dialog.FileName, photo.Data);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warning(ex, "Failed to save player photo");
            MessageWindow.Show(
                owner,
                localization.GetString("PlayerInfo_SaveImageFailed_Title"),
                localization.GetString("PlayerInfo_SaveImageFailed_Message"),
                localization.GetString("PlayerInfo_SaveImageFailed_Detail"));
        }
    }

    private void CopyImageMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_menuPhoto is not { } photo)
        {
            return;
        }

        try
        {
            Clipboard.SetImage(photo.Image);
        }
        catch (ExternalException ex)
        {
            Log.Warning(ex, "Failed to copy player photo to clipboard");
            var localization = LocalizationManager.Instance;
            MessageWindow.Show(
                Window.GetWindow(this),
                localization.GetString("PlayerInfo_CopyImageFailed_Title"),
                localization.GetString("PlayerInfo_CopyImageFailed_Message"),
                localization.GetString("PlayerInfo_CopyImageFailed_Detail"));
        }
    }

    /// <summary>リンクの最後の部分(<c>%</c> の符号を戻したもの)。リンクが読めなければ空。</summary>
    private static string GetLinkFileName(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? Path.GetFileName(Uri.UnescapeDataString(uri.AbsolutePath))
            : string.Empty;
    }
}
