using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using LevelVault.Models;
using LevelVault.Services;
using LevelVault.Providers;
using LevelVault.Views;

namespace LevelVault;

public partial class MainWindow : Window
{
    private readonly InvestigationStore _store = new();
    private readonly ProviderRegistry _providers = new();
    private readonly JsonResponseCache _cache = new();
    private readonly EvidenceAnalysisService _analysis = new();
    private readonly ObservableCollection<Investigation> _investigations = [];
    private Investigation? _active;
    private CancellationTokenSource? _researchCancellation;

    public MainWindow()
    {
        InitializeComponent();
        _providers.Register(new InternetArchiveProvider());
        _providers.Register(new GitHubRepositoryProvider());
        _providers.Register(new DuckDuckGoSearchProvider());
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
        if (_researchCancellation is not null) return;
        if (string.IsNullOrWhiteSpace(_active.Query)) { StatusText.Text = "add a search clue first."; return; }

        var investigation = _active;
        _researchCancellation = new CancellationTokenSource();
        var token = _researchCancellation.Token;
        DeepResearchButton.IsEnabled = false;
        CancelResearchButton.IsEnabled = true;
        StatusText.Text = "starting deep research...";

        var progress = new Progress<ResearchProgress>(update =>
        {
            StatusText.Text = $"deep research · query {update.QueriesCompleted + 1}/{update.QueryLimit} · depth {update.Depth + 1}/{DeepResearchPlanner.MaxDepth + 1}\n" +
                $"{update.CurrentQuery}\n{update.Detail} · {update.CluesAdded} new clue(s)";
        });

        try
        {
            var service = new InvestigationResearchService(_providers, _cache);
            var summary = await service.ResearchAsync(
                investigation,
                progress,
                checkpoint: ct => _store.SaveAsync(investigation, ct),
                cancellationToken: token);

            var graph = _analysis.Analyze(investigation);
            await _store.SaveAsync(investigation);
            RefreshActive();
            StatusText.Text = $"deep research complete: {summary.QueriesExecuted} queries, {summary.ProvidersChecked} provider checks ({summary.ProvidersSucceeded} succeeded, {summary.ProvidersFailed} failed), {summary.CluesAdded} new clue(s). " +
                $"{summary.FollowUpsRemaining} follow-up(s) remain. graph: {graph.NodeCount} nodes, {graph.RelationshipCount} relationships.";
            var providerReport = summary.Messages
                .GroupBy(message => message.Split(':', 2)[0], StringComparer.OrdinalIgnoreCase)
                .Select(group => group.Key + Environment.NewLine + string.Join(Environment.NewLine,
                    group.Select(message => message.Contains(':') ? message[(message.IndexOf(':') + 1)..].Trim() : message)
                        .Distinct(StringComparer.OrdinalIgnoreCase).Select(message => "  • " + message)));
            MessageBox.Show(
                $"queries executed: {summary.QueriesExecuted}\nprovider checks: {summary.ProvidersChecked}\nsucceeded: {summary.ProvidersSucceeded}\nfailed: {summary.ProvidersFailed}\nnew clues: {summary.CluesAdded}\n\n" +
                string.Join("\n\n", providerReport) +
                (summary.CluesAdded == 0 ? "\n\nno clues were found. try a shorter name, creator username, song id, or another known clue. a successful empty search is not proof that a level never existed." : ""),
                "levelvault research report", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            if (investigation == _active) RefreshActive();
            StatusText.Text = "deep research paused. collected clues and pending searches were saved. run deep research again to resume.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "deep research paused after an error: " + ex.Message;
        }
        finally
        {
            _researchCancellation?.Dispose();
            _researchCancellation = null;
            DeepResearchButton.IsEnabled = true;
            CancelResearchButton.IsEnabled = false;
        }
    }

    private async void ScanRecovery_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null) { StatusText.Text = "create or select an investigation first."; return; }
        StatusText.Text = "scanning local files and public archive records...";
        try
        {
            var service = new LevelRecoveryService();
            var progress = new Progress<string>(message => StatusText.Text = message);
            var summary = await service.ScanAsync(_active, progress);
            await _store.SaveAsync(_active);
            RefreshActive();
            StatusText.Text = $"recovery scan complete: {summary.FilesChecked} file(s) inspected, {summary.CandidatesAdded} new candidate(s), " +
                $"{summary.ValidatedCandidates} validated and {summary.NonValidatedCandidates} partial/unvalidated. " +
                "metadata and unrecognized files are never treated as recovered level data.";
        }
        catch (Exception ex) { StatusText.Text = "recovery scan stopped: " + ex.Message; }
    }

    private void ExportStudio_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null) { StatusText.Text = "select an investigation first."; return; }
        new ExportStudioWindow(_active.RecoveryCandidates) { Owner = this }.ShowDialog();
    }

    private void CancelResearch_Click(object sender, RoutedEventArgs e)
    {
        _researchCancellation?.Cancel();
        StatusText.Text = "cancelling after the current safe checkpoint...";
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
        var summary = _analysis.Analyze(_active);
        await _store.SaveAsync(_active);
        RefreshActive();
        StatusText.Text = $"clue saved and analyzed: {summary.NodeCount} graph nodes, {summary.RelationshipCount} relationships.";
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
        var graph = _analysis.Analyze(_active);
        await _store.SaveAsync(_active);
        RefreshActive();
        StatusText.Text = $"checked {dialog.FileNames.Length} file(s), graph now has {graph.NodeCount} nodes. validation confirms structure, not authorship or completeness.";
    }

    private async void AnalyzeClues_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null) { StatusText.Text = "create or select an investigation first."; return; }
        var summary = _analysis.Analyze(_active);
        await _store.SaveAsync(_active);
        RefreshActive();
        StatusText.Text = $"analysis complete: {summary.ExtractedMentions} mentions, {summary.CorroboratedEntities} corroborated entities, {summary.DuplicatePairs} duplicate pair(s). {summary.ConfidenceNote}";
    }

    private async void ViewGraph_Click(object sender, RoutedEventArgs e)
    {
        if (_active is null) { StatusText.Text = "select an investigation first."; return; }
        if (_active.EvidenceNodes.Count == 0) _analysis.Analyze(_active);
        await _store.SaveAsync(_active);
        new EvidenceGraphWindow(_active) { Owner = this }.ShowDialog();
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
            : $"{_active.Clues.Count} clue(s) · {_active.EvidenceNodes.Count} graph nodes · {_active.EvidenceEdges.Count} relationships · research: {_active.ResearchStatus} · created {_active.CreatedUtc.ToLocalTime():g}";
        ClueGrid.ItemsSource = _active?.Clues;
        EmptyCluesText.Visibility = _active is null || _active.Clues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
