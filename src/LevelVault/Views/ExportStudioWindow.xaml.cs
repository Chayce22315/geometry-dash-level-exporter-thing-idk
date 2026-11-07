using System.Windows;
using LevelVault.Models;
using LevelVault.Services;
using Microsoft.Win32;

namespace LevelVault.Views;

public partial class ExportStudioWindow : Window
{
    private readonly LevelExportService _exporter = new();

    public ExportStudioWindow(IReadOnlyCollection<RecoveryCandidate> candidates)
    {
        InitializeComponent();
        CandidateGrid.ItemsSource = candidates;
        CandidateGrid.SelectedIndex = candidates.Count > 0 ? 0 : -1;
        StatusText.Text = candidates.Count == 0
            ? "no recovery candidates yet. run a recovery scan first."
            : $"{candidates.Count} candidate(s) in this investigation. only validated entries are exportable.";
    }

    private async void ExportSelected_Click(object sender, RoutedEventArgs e)
    {
        if (CandidateGrid.SelectedItem is not RecoveryCandidate candidate)
        {
            StatusText.Text = "select a recovery candidate first.";
            return;
        }

        if (!string.Equals(candidate.Status, "validated", StringComparison.OrdinalIgnoreCase))
        {
            StatusText.Text = "this candidate is not validated. partial or unverified data cannot be exported as an authentic .gmd.";
            return;
        }

        var safeName = string.Concat((string.IsNullOrWhiteSpace(candidate.LevelName) ? candidate.Title : candidate.LevelName)
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "recovered-level";

        var dialog = new SaveFileDialog
        {
            Title = "export original geometry dash level",
            Filter = "geometry dash level (*.gmd)|*.gmd",
            DefaultExt = ".gmd",
            AddExtension = true,
            FileName = safeName + ".gmd",
            OverwritePrompt = true
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            StatusText.Text = "rechecking original payload and preparing export...";
            var result = await _exporter.ExportValidatedCandidateAsync(candidate, dialog.FileName);
            StatusText.Text = $"export verified. original bytes preserved. sha-256: {result.RawFileSha256}";
            MessageBox.Show(this,
                $"level exported successfully.\n\n{result.LevelPath}\n\nprovenance manifest:\n{result.ManifestPath}\n\nstructural validation does not independently prove authorship or completeness.",
                "export complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            StatusText.Text = "export failed: " + ex.Message;
            MessageBox.Show(this, ex.Message, "export failed", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
