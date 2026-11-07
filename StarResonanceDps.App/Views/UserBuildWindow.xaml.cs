using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using StarResonanceDps.App.Models;
using StarResonanceDps.App.Services;

namespace StarResonanceDps.App.Views;

public partial class UserBuildWindow : Window
{
    private readonly UserBuildStore _store = new();
    private UserBuild _current = new();
    private bool _loading;

    public UserBuildWindow()
    {
        InitializeComponent();
        LoadEditor(_current);
        try
        {
            BuildList.ItemsSource = _store.Load(out var failed);
            if (failed > 0) StatusText.Text = $"{failed}件を読み込めませんでした。元のファイルは保持しています。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"読み込みに失敗しました：{ex.Message}";
        }
        Closing += OnClosing;
    }

    private bool HasChanges => NameBox.Text != _current.Name
        || MindscapeBox.Text != _current.Mindscape || AbilitiesBox.Text != _current.Abilities
        || ImaginesBox.Text != _current.Imagines || TimelineBox.Text != _current.SkillTimeline;

    private bool CanLeave() => !HasChanges || MessageBox.Show(this,
        "未保存の変更を破棄しますか？", "ユーザービルド", MessageBoxButton.YesNo,
        MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    private void OnClosing(object? sender, CancelEventArgs e) => e.Cancel = !CanLeave();

    private void LoadEditor(UserBuild build)
    {
        _current = build;
        NameBox.Text = build.Name;
        MindscapeBox.Text = build.Mindscape;
        AbilitiesBox.Text = build.Abilities;
        ImaginesBox.Text = build.Imagines;
        TimelineBox.Text = build.SkillTimeline;
    }

    private void BuildList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || BuildList.SelectedItem is not UserBuild selected) return;
        if (!CanLeave())
        {
            _loading = true;
            BuildList.SelectedItem = _current;
            _loading = false;
            return;
        }
        LoadEditor(selected);
        StatusText.Text = $"保存日時：{selected.UpdatedUtc.ToLocalTime():yyyy/MM/dd HH:mm:ss}";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!CanLeave()) return;
        _loading = true;
        BuildList.SelectedItem = null;
        _loading = false;
        LoadEditor(new UserBuild());
        StatusText.Text = "新規ビルド";
        NameBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save(duplicate: false);
    private void Duplicate_Click(object sender, RoutedEventArgs e) => Save(duplicate: true);

    private void Save(bool duplicate)
    {
        var build = new UserBuild
        {
            Id = duplicate ? Guid.NewGuid() : _current.Id,
            Name = NameBox.Text.Trim() + (duplicate ? " (コピー)" : ""),
            Mindscape = MindscapeBox.Text,
            Abilities = AbilitiesBox.Text,
            Imagines = ImaginesBox.Text,
            SkillTimeline = TimelineBox.Text
        };
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            StatusText.Text = "ビルド名を入力してください。";
            NameBox.Focus();
            return;
        }
        try
        {
            _store.Save(build);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"保存に失敗しました：{ex.Message}";
            return;
        }
        // 読み込み失敗があっても、完了済みの保存と区別する。
        LoadEditor(build);
        _loading = true;
        try
        {
            BuildList.ItemsSource = _store.Load(out var failed);
            BuildList.SelectedItem = BuildList.Items.Cast<UserBuild>().FirstOrDefault(x => x.Id == build.Id);
            StatusText.Text = failed == 0 ? "保存しました。" : $"保存しました。ほかの{failed}件は読み込めませんでした。";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            StatusText.Text = $"保存しました。一覧の更新に失敗しました：{ex.Message}";
        }
        finally { _loading = false; }
    }
}
