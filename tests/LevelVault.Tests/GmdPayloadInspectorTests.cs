using System.IO.Compression;
using System.Text;
using LevelVault.Models;
using LevelVault.Services;
using Xunit;

namespace LevelVault.Tests;

public sealed class GmdPayloadInspectorTests
{
    [Fact]
    public async Task ValidPlainPayloadIsRecognizedAndHasStableHashes()
    {
        var path = await WritePlistAsync("1,2,3,4");
        try
        {
            var result = await GmdPayloadInspector.InspectFileAsync(path);
            Assert.True(result.IsRecognized);
            Assert.True(result.PayloadValidated);
            Assert.Equal("demo level", result.LevelName);
            Assert.Equal("12345", result.LevelId);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(await File.ReadAllBytesAsync(path))), result.RawFileSha256);
            Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes("1,2,3,4"))), result.PayloadSha256);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ValidGzipPayloadIsExpandedAndValidated()
    {
        var payload = Encoding.UTF8.GetBytes("1,2,3,4");
        await using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.Optimal, true))
            await gzip.WriteAsync(payload);
        var path = await WritePlistWithEncodedPayloadAsync(Convert.ToBase64String(compressed.ToArray()));
        try
        {
            var result = await GmdPayloadInspector.InspectFileAsync(path);
            Assert.True(result.IsRecognized);
            Assert.True(result.PayloadValidated);
            Assert.Equal(payload.LongLength, result.PayloadBytes);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task InvalidBase64IsNotAcceptedAsRecoveredData()
    {
        var path = await WritePlistWithEncodedPayloadAsync("not base64!");
        try
        {
            var result = await GmdPayloadInspector.InspectFileAsync(path);
            Assert.False(result.IsRecognized);
            Assert.False(result.PayloadValidated);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MalformedObjectPairsRemainUnvalidated()
    {
        var path = await WritePlistAsync("1,2,not-a-key,4");
        try
        {
            var result = await GmdPayloadInspector.InspectFileAsync(path);
            Assert.True(result.IsRecognized);
            Assert.False(result.PayloadValidated);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task OversizedInputIsRejectedBeforeParsing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".plist");
        await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
            file.SetLength(GmdPayloadInspector.MaxInputBytes + 1);
        try
        {
            var result = await GmdPayloadInspector.InspectFileAsync(path);
            Assert.False(result.IsRecognized);
            Assert.Contains("25 MiB", result.Summary);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CorruptGzipPayloadIsNotValidated()
    {
        var corruptGzip = Convert.ToBase64String(new byte[] { 0x1f, 0x8b, 0x08, 0x00, 0x01, 0x02, 0x03 });
        var path = await WritePlistWithEncodedPayloadAsync(corruptGzip);
        try
        {
            var result = await GmdPayloadInspector.InspectFileAsync(path);
            Assert.False(result.PayloadValidated);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task NonPlistInputIsRejected()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "<html>not a plist</html>");
        try
        {
            var result = await GmdPayloadInspector.InspectFileAsync(path);
            Assert.False(result.IsRecognized);
            Assert.False(result.PayloadValidated);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task ExportRechecksAndPreservesOriginalBytesAndWritesManifest()
    {
        var source = await WritePlistAsync("1,2,3,4");
        var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gmd");
        try
        {
            var inspection = await GmdPayloadInspector.InspectFileAsync(source);
            var candidate = new RecoveryCandidate
            {
                Title = "demo.plist", SourceUrl = "https://archive.org/details/example/demo.plist",
                LocalPath = source, Format = "plist", LevelName = "demo level", LevelId = "12345",
                Status = "validated", RawFileSha256 = inspection.RawFileSha256,
                PayloadSha256 = inspection.PayloadSha256
            };
            var result = await new LevelExportService().ExportValidatedCandidateAsync(candidate, output);
            Assert.Equal(inspection.RawFileSha256, result.RawFileSha256);
            Assert.Equal(await File.ReadAllBytesAsync(source), await File.ReadAllBytesAsync(output));
            Assert.True(File.Exists(result.ManifestPath));
            Assert.Contains("authenticityNote", await File.ReadAllTextAsync(result.ManifestPath));
        }
        finally
        {
            if (File.Exists(source)) File.Delete(source);
            if (File.Exists(output)) File.Delete(output);
            if (File.Exists(output + ".manifest.json")) File.Delete(output + ".manifest.json");
        }
    }

    [Fact]
    public async Task ExportRefusesPartialCandidate()
    {
        var source = await WritePlistAsync("1,2,3,4");
        var output = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".gmd");
        try
        {
            var candidate = new RecoveryCandidate { LocalPath = source, Status = "partial" };
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new LevelExportService().ExportValidatedCandidateAsync(candidate, output));
            Assert.False(File.Exists(output));
        }
        finally { File.Delete(source); }
    }

    private static Task<string> WritePlistAsync(string payload) =>
        WritePlistWithEncodedPayloadAsync(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)));

    private static async Task<string> WritePlistWithEncodedPayloadAsync(string encodedPayload)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".plist");
        var plist = $"<?xml version=\"1.0\"?><plist version=\"1.0\"><dict><key>k1</key><integer>12345</integer><key>k2</key><string>demo level</string><key>k4</key><string>{encodedPayload}</string></dict></plist>";
        await File.WriteAllTextAsync(path, plist);
        return path;
    }
}
