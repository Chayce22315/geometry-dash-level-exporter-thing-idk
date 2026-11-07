using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LevelVault.Models;
using LevelVault.Services;
using LevelVault.Providers;

namespace LevelVault;

public partial class MainWindow : Window
{
    private readonly InvestigationStore _store = new();
    private readonly ProviderRegistry _providers = new();
    private readonly JsonResponseCache _cache = new();
    private readonly ObservableCollection<Investigation> _investigations = [];
    private Investigation? _active;

    public MainWindow()
    {
        InitializeComponent();
        _providers.Register(new InternetArchiveProvider());
        InvestigationList.ItemsSource = _investigations;
        Loaded += async (_, _) =>
        {
            foreach (var item in await _store.LoadAllAsync()) _investigations.Add(item);
            if (_investigations.Count > 0) InvestigationList.SelectedIndex = 0;
        };
    }

    private async void CreateInvestigation_Click(object sender, RoutedEventArgs e)
    {
        var query = QueryBox.Text.Trim();
        if (query.Length < 2) { StatusText.Text = "enter at least two characters to start an investigation."; return; }
        var item = new Investigation { Query = query };
        _investigations.Insert(0, item);
        InvestigationList.SelectedItem = item;
        await _store.SaveAsync(item);
        StatusText.Text = "investigation created and saved locally.";
    }

    private async void ResearchArchives_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null) { StatusText.Text = "create or select an investigation first."; return; }
        if (string.IsNullOrWhiteSpace(_active.Query)) { StatusText.Text = "add a search clue first."; return; }

        StatusText.Text = "searching configured public archives... this may take a moment.";
        try
        {
            var service = new InvestigationResearchService(_providers, _cache);
            var summary = await service.ResearchAsync(_active);
            await _store.SaveAsync(_active);
            RefreshActive();
            StatusText.Text = $"research complete: {summary.CluesAdded} new clue(s), " +
                $"{summary.ProvidersSucceeded}/{summary.ProvidersChecked} provider(s) responded. " +
                string.Join(" ", summary.Messages);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "archive research cancelled.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "archive research failed: " + ex.Message;
        }
    }

    private void OpenSearchSet_Click(object sender, RoutedEventArgs e)
    {
        var query = _active?.Query ?? QueryBox.Text.Trim();
        var targets = SearchPlanner.BuildTargets(query);
        if (targets.Count == 0) { StatusText.Text = "enter a search clue first."; return; }
        foreach (var target in targets)
        {
            var result = MessageBox.Show($"open {target.Label}?\n\n{target.Url}", "levelvault search", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes) SearchPlanner.Open(target);
        }
        StatusText.Text = "search links opened when approved. add useful discoveries as clues.";
    }

    private async void AddClue_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null) { StatusText.Text = "create or select an investigation first."; return; }
        var title = Prompt("clue title", "add clue");
        if (string.IsNullOrWhiteSpace(title)) return;
        var url = Prompt("source url (optional)", "source");
        var detail = Prompt("notes / evidence (optional)", "details");
        _active.Clues.Add(new Clue { Title = title.Trim(), SourceUrl = url.Trim(), Detail = detail.Trim() });
        await _store.SaveAsync(_active);
        RefreshActive();
        StatusText.Text = "clue saved. source and notes are retained with the investigation.";
    }

    private async void ImportGmd_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null) { StatusText.Text = "select an investigation first."; return; }
        var dialog = new OpenFileDialog { Filter = "Geometry Dash plist exports (*.gmd;*.plist)|*.gmd;*.plist|All files (*.*)|*.*", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        foreach (var path in dialog.FileNames)
        {
            var result = GmdValidator.Validate(path);
            _active.Clues.Add(new Clue
            {
                Kind = result.IsValid ? "level file" : "file",
                Title = Path.GetFileName(path) + " · " + result.Summary,
                Detail = string.Join(", ", result.Keys),
                SourceUrl = path,
                Confidence = result.IsValid ? "structure valid" : "unverified"
            });
        }
        await _store.SaveAsync(_active);
        RefreshActive();
        StatusText.Text = $"checked {dialog.FileNames.Length} file(s). validation confirms structure, not authorship or completeness.";
    }

    private void InvestigationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _active = InvestigationList.SelectedItem as Investigation;
        RefreshActive();
    }

    private void RefreshActive()
    {
        ActiveTitle.Text = _active?.Query ?? "no investigation selected";
        ActiveSubtitle.Text = _active is null ? "create an investigation to begin collecting evidence."
            : $"{_active.Clues.Count} clue(s) · created {_active.CreatedUtc.ToLocalTime():g}";
        ClueGrid.ItemsSource = _active?.Clues;
    }

    private static string Prompt(string message, string title)
    {
        var window = new Window { Title = title, Width = 480, Height = 190, WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Application.Current.MainWindow, Background = System.Windows.Media.Brushes.DimGray, ResizeMode = ResizeMode.NoResize };
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = message, Margin = new Thickness(0,0,0,8), Foreground = System.Windows.Media.Brushes.White });
        var input = new TextBox { MinHeight = 34 };
        panel.Children.Add(input);
        var button = new Button { Content = "save", Width = 90, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,12,0,0), IsDefault = true };
        button.Click += (_, _) => window.DialogResult = true;
        panel.Children.Add(button);
        window.Content = panel;
        return window.ShowDialog() == true ? input.Text : "";
    }
}
