using System.Net;
using System.Net.Http;
using System.Text.Json;
using LevelVault.Models;

namespace LevelVault.Services;

/// <summary>Scans local clue files and public Internet Archive item records for original GD plist exports.</summary>
public sealed class LevelRecoveryService
{
    private static readonly HttpClient Client = CreateClient();
    private const long MaxBytes = GmdPayloadInspector.MaxInputBytes;

    public async Task<RecoveryRunSummary> ScanAsync(Investigation investigation,IProgress<string>? progress=null,CancellationToken ct=default)
    {
        var checkedFiles=0; var added=0; var failures=new List<string>();
        var seen=new HashSet<string>(investigation.RecoveryCandidates.Select(x=>x.SourceUrl),StringComparer.OrdinalIgnoreCase);
        foreach(var clue in investigation.Clues.ToArray())
        {
            ct.ThrowIfCancellationRequested();
            if(File.Exists(clue.SourceUrl) && Supported(clue.SourceUrl))
            {
                var item=await InspectAndAdd(investigation,clue.SourceUrl,clue.SourceUrl,ct);
                checkedFiles++; if(item) added++;
                continue;
            }
            if(!TryArchiveId(clue.SourceUrl,out var identifier) || !seen.Add("archive:"+identifier)) continue;
            try
            {
                progress?.Report("checking public archive item: "+identifier);
                using var meta=await Client.GetAsync("https://archive.org/metadata/"+Uri.EscapeDataString(identifier),ct);
                if(meta.StatusCode==HttpStatusCode.NotFound) continue;
                meta.EnsureSuccessStatusCode();
                await using var ms=await meta.Content.ReadAsStreamAsync(ct);
                using var json=await JsonDocument.ParseAsync(ms,cancellationToken:ct);
                if(!json.RootElement.TryGetProperty("files",out var files) || files.ValueKind!=JsonValueKind.Array) continue;
                foreach(var f in files.EnumerateArray())
                {
                    ct.ThrowIfCancellationRequested();
                    var name=f.TryGetProperty("name",out var n)?n.GetString():null;
                    if(string.IsNullOrWhiteSpace(name)||!Supported(name)) continue;
                    if(f.TryGetProperty("size",out var sz)&&long.TryParse(sz.ToString(),out var size)&&(size<=0||size>MaxBytes)) continue;
                    var url="https://archive.org/download/"+Uri.EscapeDataString(identifier)+"/"+
                        string.Join("/",name.Split('/').Select(Uri.EscapeDataString));
                    if(!seen.Add(url)) continue;
                    progress?.Report("retrieving candidate: "+Path.GetFileName(name));
                    using var response=await Client.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);
                    if(!response.IsSuccessStatusCode){failures.Add(name+": http "+(int)response.StatusCode);continue;}
                    if(response.Content.Headers.ContentLength is > MaxBytes) continue;
                    await using var stream=await response.Content.ReadAsStreamAsync(ct);
                    await using var buffer=new MemoryStream();
                    var chunk=new byte[81920];
                    var oversized=false;
                    while(true){var count=await stream.ReadAsync(chunk,ct);if(count==0)break;
                        if(buffer.Length+count>MaxBytes){oversized=true;break;}
                        await buffer.WriteAsync(chunk.AsMemory(0,count),ct);}
                    if(oversized){failures.Add(name+": download exceeded the 25 MiB limit; skipped.");continue;}
                    var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "LevelVault","recovered-candidates",Safe(identifier));
                    Directory.CreateDirectory(folder);
                    var local=Path.Combine(folder,Safe(Path.GetFileName(name)));
                    await File.WriteAllBytesAsync(local,buffer.ToArray(),ct);
                    checkedFiles++; if(await InspectAndAdd(investigation,local,url,ct)) added++;
                }
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
            catch(Exception ex) when(ex is HttpRequestException or JsonException or IOException or InvalidOperationException)
            {failures.Add(identifier+": "+ex.Message);}
        }
        return new(checkedFiles,added,investigation.RecoveryCandidates.Count(x=>x.Status=="validated"),
            investigation.RecoveryCandidates.Count(x=>x.Status!="validated"),failures);
    }

    private static async Task<bool> InspectAndAdd(Investigation inv,string path,string source,CancellationToken ct)
    {
        var info=new FileInfo(path); if(!info.Exists||info.Length>MaxBytes)return false;
        var result=await GmdPayloadInspector.InspectFileAsync(path,ct);
        if(!result.IsRecognized)return false;
        if(inv.RecoveryCandidates.Any(x=>x.RawFileSha256.Length>0&&x.RawFileSha256.Equals(result.RawFileSha256,StringComparison.OrdinalIgnoreCase)))return false;
        var candidate=new RecoveryCandidate{Title=Path.GetFileName(path),SourceUrl=source,LocalPath=path,
            Format=Path.GetExtension(path).TrimStart('.').ToLowerInvariant(),LevelName=result.LevelName,LevelId=result.LevelId,
            Status=result.PayloadValidated?"validated":"partial",ValidationSummary=result.Summary,
            RawFileSha256=result.RawFileSha256,PayloadSha256=result.PayloadSha256,PayloadBytes=result.PayloadBytes,
            RetrievedUtc=source.StartsWith("https://",StringComparison.OrdinalIgnoreCase)?DateTime.UtcNow:null};
        inv.RecoveryCandidates.Add(candidate);
        inv.Clues.Add(new Clue{Kind="recovery candidate",Title=candidate.Title+" · "+candidate.Status,
            Detail=result.Summary+" raw sha256: "+result.RawFileSha256,SourceUrl=source,Confidence=candidate.Status});
        return true;
    }

    private static bool TryArchiveId(string? value,out string id)
    {
        id=""; if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Host is not ("archive.org" or "www.archive.org"))return false;
        var parts=uri.AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries);
        if(parts.Length<2||parts[0]!="details")return false;
        id=Uri.UnescapeDataString(parts[1]);return id.Length is >0 and <=200;
    }
    private static bool Supported(string path)=>new[]{".gmd",".plist",".xml"}.Contains(Path.GetExtension(path),StringComparer.OrdinalIgnoreCase);
    private static string Safe(string value){var bad=Path.GetInvalidFileNameChars();var s=new string(value.Select(c=>bad.Contains(c)?'_':c).ToArray());return s.Length>100?s[..100]:s;}
    private static HttpClient CreateClient(){var c=new HttpClient{Timeout=TimeSpan.FromSeconds(30)};c.DefaultRequestHeaders.UserAgent.ParseAdd("LevelVault/0.3 (public archive recovery; github.com/Chayce22315)");return c;}
}
public sealed record RecoveryRunSummary(int FilesChecked,int CandidatesAdded,int ValidatedCandidates,int NonValidatedCandidates,IReadOnlyList<string> Failures);
