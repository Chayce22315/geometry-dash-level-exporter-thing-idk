using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace LevelVault.Services;

public sealed record GmdInspectionResult(bool IsRecognized, bool PayloadValidated, string Summary,
    string LevelName, string LevelId, string RawFileSha256, string PayloadSha256, long PayloadBytes);

public static class GmdPayloadInspector
{
    public const long MaxInputBytes = 25 * 1024 * 1024;
    private const int MaxExpandedBytes = 64 * 1024 * 1024;

    public static async Task<GmdInspectionResult> InspectFileAsync(string path, CancellationToken ct = default)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return Fail("file does not exist.");
        if (info.Length > MaxInputBytes) return Fail("file exceeds the 25 MiB inspection limit.");
        var bytes = await File.ReadAllBytesAsync(path, ct);
        var rawHash = Convert.ToHexString(SHA256.HashData(bytes));
        try
        {
            var doc = XDocument.Parse(Encoding.UTF8.GetString(bytes));
            var dict = doc.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "dict");
            if (doc.Root?.Name.LocalName != "plist" || dict is null)
                return Fail("not a plist dictionary.", rawHash);
            var values = new Dictionary<string,string>();
            var nodes = dict.Elements().ToArray();
            for (var i=0; i+1<nodes.Length; i++)
            {
                if (nodes[i].Name.LocalName != "key") continue;
                if (nodes[i+1].Name.LocalName is "string" or "integer")
                    values[nodes[i].Value] = nodes[i+1].Value;
                i++;
            }
            if (!values.TryGetValue("k4", out var encoded) || string.IsNullOrWhiteSpace(encoded))
                return Fail("plist contains no k4 level payload.", rawHash);
            var name = values.GetValueOrDefault("k2") ?? "";
            var id = values.GetValueOrDefault("k1") ?? "";
            if (!id.All(char.IsDigit)) id = "";
            byte[] decoded;
            try { decoded = Convert.FromBase64String(encoded.Trim()); }
            catch (FormatException) { return Fail("k4 is not valid base64.", rawHash, name, id); }

            byte[] payload;
            if (decoded.Length >= 2 && decoded[0] == 0x1f && decoded[1] == 0x8b)
            {
                await using var input = new MemoryStream(decoded);
                await using var gzip = new GZipStream(input, CompressionMode.Decompress);
                await using var output = new MemoryStream();
                var buffer = new byte[81920];
                while (true)
                {
                    var count = await gzip.ReadAsync(buffer, ct);
                    if (count == 0) break;
                    if (output.Length + count > MaxExpandedBytes)
                        return Fail("decompressed payload exceeds 64 MiB.", rawHash, name, id);
                    await output.WriteAsync(buffer.AsMemory(0,count), ct);
                }
                payload = output.ToArray();
            }
            else payload = decoded;

            var text = new UTF8Encoding(false,true).GetString(payload);
            var tokens = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var valid = tokens.Length >= 4 && tokens.Length % 2 == 0 &&
                Enumerable.Range(0,tokens.Length/2).All(i => tokens[i*2].All(char.IsDigit));
            var payloadHash = Convert.ToHexString(SHA256.HashData(payload));
            return new(true, valid, valid
                ? $"plist and k4 payload validated; {tokens.Length/2:N0} object key/value pairs."
                : "plist and k4 decoded, but payload does not match the expected comma-separated object structure.",
                name,id,rawHash,payloadHash,payload.LongLength);
        }
        catch (Exception ex) when (ex is System.Xml.XmlException or DecoderFallbackException or InvalidDataException or IOException or ArgumentException)
        {
            return Fail("inspection failed: " + ex.Message,rawHash);
        }
    }

    private static GmdInspectionResult Fail(string message,string hash="",string name="",string id="") =>
        new(false,false,message,name,id,hash,"",0);
}
