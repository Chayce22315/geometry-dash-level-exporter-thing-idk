using System.Security.Cryptography;
using System.Text.Json;
using LevelVault.Models;

namespace LevelVault.Services;

public sealed record LevelExportResult(string LevelPath, string ManifestPath, string RawFileSha256, string PayloadSha256);

/// <summary>Exports only re-inspected, structurally validated original level files without rewriting their bytes.</summary>
public sealed class LevelExportService
{
    public async Task<LevelExportResult> ExportValidatedCandidateAsync(
        RecoveryCandidate candidate, string destinationPath, CancellationToken ct = default)
    {
        if (!string.Equals(candidate.Status, "validated", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only candidates marked validated can be exported as a .gmd level.");

        if (string.IsNullOrWhiteSpace(candidate.LocalPath) || !File.Exists(candidate.LocalPath))
            throw new FileNotFoundException("The original candidate file is no longer available.", candidate.LocalPath);

        if (!string.Equals(Path.GetExtension(destinationPath), ".gmd", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The export destination must use the .gmd extension.", nameof(destinationPath));

        var sourcePath = Path.GetFullPath(candidate.LocalPath);
        var outputPath = Path.GetFullPath(destinationPath);
        if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Choose a destination separate from the original candidate.");

        var sourceInspection = await GmdPayloadInspector.InspectFileAsync(sourcePath, ct);
        if (!sourceInspection.IsRecognized || !sourceInspection.PayloadValidated)
            throw new InvalidDataException("The source no longer passes level payload validation.");

        if (!string.IsNullOrWhiteSpace(candidate.RawFileSha256) &&
            !string.Equals(candidate.RawFileSha256, sourceInspection.RawFileSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The source file changed after it was added to this investigation.");

        if (!string.IsNullOrWhiteSpace(candidate.PayloadSha256) &&
            !string.Equals(candidate.PayloadSha256, sourceInspection.PayloadSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The decoded level payload does not match the saved recovery record.");

        var bytes = await File.ReadAllBytesAsync(sourcePath, ct);
        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);
        await File.WriteAllBytesAsync(outputPath, bytes, ct);

        var exportedInspection = await GmdPayloadInspector.InspectFileAsync(outputPath, ct);
        var copiedHash = Convert.ToHexString(SHA256.HashData(bytes));
        if (!exportedInspection.IsRecognized || !exportedInspection.PayloadValidated ||
            !string.Equals(copiedHash, exportedInspection.RawFileSha256, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(sourceInspection.RawFileSha256, exportedInspection.RawFileSha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(outputPath);
            throw new InvalidDataException("The exported copy failed round-trip validation and was removed.");
        }

        var manifestPath = outputPath + ".manifest.json";
        var manifest = new
        {
            schema = "levelvault-export-manifest-v1",
            exportedUtc = DateTime.UtcNow,
            file = Path.GetFileName(outputPath),
            originalFormat = candidate.Format,
            levelName = sourceInspection.LevelName,
            levelId = sourceInspection.LevelId,
            sourceUrl = candidate.SourceUrl,
            originalLocalPath = sourcePath,
            rawFileSha256 = exportedInspection.RawFileSha256,
            payloadSha256 = exportedInspection.PayloadSha256,
            payloadBytes = exportedInspection.PayloadBytes,
            validation = exportedInspection.Summary,
            authenticityNote = "Structural validation confirms the supported plist and level payload format. It does not independently prove authorship, completeness, or official server provenance."
        };
        await File.WriteAllTextAsync(manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }), ct);

        return new(outputPath, manifestPath, exportedInspection.RawFileSha256, exportedInspection.PayloadSha256);
    }
}
