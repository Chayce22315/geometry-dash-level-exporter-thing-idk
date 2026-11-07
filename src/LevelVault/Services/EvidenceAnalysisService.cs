using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LevelVault.Models;

namespace LevelVault.Services;

/// <summary>
/// Builds a provenance-preserving evidence graph from investigation clues.
/// Extraction is deliberately conservative: matches are leads, never proof.
/// </summary>
public sealed class EvidenceAnalysisService
{
    private static readonly Regex LevelIdPattern = new(@"\blevel\s*(?:id)?\s*[:#=]\s*(\d{4,12})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SongIdPattern = new(@"\b(?:song(?:\s*id)?|custom\s*song)\s*[:#=]\s*(\d{1,10})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CreatorPattern = new(@"\b(?:creator|created\s*by|author|username)\s*[:=@]\s*([A-Za-z0-9_-]{2,32})", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UrlPattern = new(@"https?://[^\s<>""']+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public EvidenceAnalysisSummary Analyze(Investigation investigation)
    {
        ArgumentNullException.ThrowIfNull(investigation);
        investigation.EvidenceNodes.Clear();
        investigation.EvidenceEdges.Clear();

        var nodes = new Dictionary<string, EvidenceNode>(StringComparer.OrdinalIgnoreCase);
        var mentions = new List<(Guid ClueId, string EntityKey, string Domain, string ClueText)>();
        var target = AddNode(nodes, "target", investigation.Query, "investigation query");
        var clueNodes = new List<(Clue Clue, EvidenceNode Node, string CanonicalSource)>();

        foreach (var clue in investigation.Clues)
        {
            var clueNode = new EvidenceNode
            {
                Id = clue.Id,
                Type = "clue",
                Label = string.IsNullOrWhiteSpace(clue.Title) ? "untitled clue" : clue.Title,
                SourceUrl = clue.SourceUrl ?? "",
                Notes = clue.Detail ?? ""
            };
            nodes["clue:" + clue.Id.ToString("N")] = clueNode;
            clueNodes.Add((clue, clueNode, CanonicalizeUrl(clue.SourceUrl)));
            AddEdge(investigation, target.Id, clueNode.Id, "has-evidence",
                "this clue belongs to the selected investigation.", "unverified");
        }

        foreach (var (clue, clueNode, _) in clueNodes)
        {
            var text = string.Join(" ", clue.Title, clue.Detail, clue.SourceUrl);
            var found = new Dictionary<string, EvidenceNode>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in LevelIdPattern.Matches(text))
                AddMention(found, nodes, "level-id", match.Groups[1].Value, "level id");
            foreach (Match match in SongIdPattern.Matches(text))
                AddMention(found, nodes, "song-id", match.Groups[1].Value, "song id");
            foreach (Match match in CreatorPattern.Matches(text))
                AddMention(found, nodes, "creator", match.Groups[1].Value, "creator");

            foreach (Match match in UrlPattern.Matches(text))
            {
                var raw = match.Value.TrimEnd('.', ',', ')', ']', ';');
                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) continue;
                var kind = ClassifyUrl(uri);
                var label = kind switch
                {
                    "video" => "video reference",
                    "archive-record" => "archive record",
                    "repository" => "code repository",
                    _ => uri.Host
                };
                AddMention(found, nodes, kind, raw, label, raw);
            }

            var domain = TryGetDomain(clue.SourceUrl);
            foreach (var entity in found.Values)
            {
                AddEdge(investigation, clueNode.Id, entity.Id, "mentions",
                    $"detected {entity.Type} from clue text; this is an unverified extraction.", "unverified");
                mentions.Add((clue.Id, NodeKey(entity.Type, entity.Label), domain, text));
            }

            var level = found.Values.FirstOrDefault(x => x.Type == "level-id");
            var creator = found.Values.FirstOrDefault(x => x.Type == "creator");
            var song = found.Values.FirstOrDefault(x => x.Type == "song-id");
            var video = found.Values.FirstOrDefault(x => x.Type == "video");
            var archive = found.Values.FirstOrDefault(x => x.Type == "archive-record");

            if (level is not null && creator is not null)
                AddEdge(investigation, creator.Id, level.Id, "possibly-created",
                    "creator and level id occur in the same clue; authorship is not independently verified.", "unverified");
            if (level is not null && song is not null)
                AddEdge(investigation, level.Id, song.Id, "possibly-uses-song",
                    "level id and song id occur in the same clue; song association is not verified.", "unverified");
            if (level is not null && video is not null)
                AddEdge(investigation, video.Id, level.Id, "may-reference",
                    "video and level id occur in the same clue; video contents have not been inspected.", "unverified");
            if (level is not null && archive is not null)
                AddEdge(investigation, archive.Id, level.Id, "possible-copy-of",
                    "archive reference and level id occur together; archive contents have not been validated.", "unverified");
        }

        if (Regex.IsMatch(investigation.Query.Trim(), @"^\d{4,12}$"))
        {
            var level = AddNode(nodes, "level-id", investigation.Query.Trim(), "level id");
            AddEdge(investigation, target.Id, level.Id, "targets-level",
                "the investigation query is a numeric level id.", "unverified");
        }

        var corroborated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in mentions.GroupBy(x => x.EntityKey, StringComparer.OrdinalIgnoreCase))
        {
            var domains = group.Select(x => x.Domain).Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (domains.Length < 2) continue;
            corroborated.Add(group.Key);
            var node = nodes.Values.FirstOrDefault(x => NodeKey(x.Type, x.Label).Equals(group.Key, StringComparison.OrdinalIgnoreCase));
            if (node is not null)
            {
                node.Notes = $"corroborated by {domains.Length} distinct source domains: {string.Join(", ", domains)}. this confirms repeated references, not the underlying claim.";
                foreach (var edge in investigation.EvidenceEdges.Where(x => x.ToNodeId == node.Id && x.Relationship == "mentions"))
                {
                    edge.Confidence = "corroborated";
                    edge.Rationale = $"the same {node.Type} appears in clues from {domains.Length} distinct source domains.";
                }
            }
        }

        var duplicateGroups = clueNodes.Where(x => !string.IsNullOrWhiteSpace(x.CanonicalSource))
            .GroupBy(x => x.CanonicalSource, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1);
        var duplicatePairs = 0;
        foreach (var group in duplicateGroups)
        {
            var items = group.ToArray();
            for (var i = 0; i < items.Length; i++)
            for (var j = i + 1; j < items.Length; j++)
            {
                AddEdge(investigation, items[i].Node.Id, items[j].Node.Id, "possible-duplicate-of",
                    "both clues use the same canonical source URL. both original clues are preserved.", "strong match");
                duplicatePairs++;
            }
        }

        foreach (var node in nodes.Values) investigation.EvidenceNodes.Add(node);
        return new EvidenceAnalysisSummary(
            investigation.EvidenceNodes.Count,
            investigation.EvidenceEdges.Count,
            mentions.Count,
            corroborated.Count,
            duplicatePairs,
            "confidence describes source agreement, not proof that a level was recovered.");
    }

    private static void AddMention(Dictionary<string, EvidenceNode> found, Dictionary<string, EvidenceNode> nodes,
        string type, string value, string label, string? sourceUrl = null)
    {
        var node = AddNode(nodes, type, value, label, sourceUrl);
        found[NodeKey(type, node.Label)] = node;
    }

    private static EvidenceNode AddNode(Dictionary<string, EvidenceNode> nodes, string type, string value,
        string label, string? sourceUrl = null)
    {
        var normalized = Normalize(value);
        var key = NodeKey(type, normalized);
        if (nodes.TryGetValue(key, out var existing)) return existing;
        var node = new EvidenceNode
        {
            Id = StableGuid(key),
            Type = type,
            Label = value.Trim(),
            SourceUrl = sourceUrl ?? "",
            Notes = label
        };
        nodes[key] = node;
        return node;
    }

    private static void AddEdge(Investigation investigation, Guid from, Guid to, string relationship,
        string rationale, string confidence)
    {
        if (from == to || investigation.EvidenceEdges.Any(x =>
            x.FromNodeId == from && x.ToNodeId == to && x.Relationship == relationship)) return;
        investigation.EvidenceEdges.Add(new EvidenceEdge
        {
            Id = StableGuid($"{from:N}|{to:N}|{relationship}"),
            FromNodeId = from,
            ToNodeId = to,
            Relationship = relationship,
            Rationale = rationale,
            Confidence = confidence
        });
    }

    private static string ClassifyUrl(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host is "youtube.com" or "www.youtube.com" or "youtu.be" or "m.youtube.com" || host.EndsWith(".youtube.com"))
            return "video";
        if ((host is "archive.org" or "www.archive.org") &&
            uri.AbsolutePath.StartsWith("/details/", StringComparison.OrdinalIgnoreCase))
            return "archive-record";
        if (host is "github.com" or "www.github.com" || host.EndsWith(".github.com")) return "repository";
        return "web-reference";
    }

    private static string TryGetDomain(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Host.ToLowerInvariant() : "";

    private static string CanonicalizeUrl(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return "";
        var builder = new UriBuilder(uri) { Fragment = "" };
        var queryParts = builder.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var retained = queryParts.Where(part =>
        {
            var key = part.Split('=', 2)[0];
            return !key.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) &&
                   !key.Equals("fbclid", StringComparison.OrdinalIgnoreCase) &&
                   !key.Equals("gclid", StringComparison.OrdinalIgnoreCase);
        });
        builder.Query = string.Join("&", retained);
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }

    private static string Normalize(string value) =>
        Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();

    private static string NodeKey(string type, string value) => type + ":" + Normalize(value);

    private static Guid StableGuid(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(bytes);
    }
}

public sealed record EvidenceAnalysisSummary(
    int NodeCount,
    int RelationshipCount,
    int ExtractedMentions,
    int CorroboratedEntities,
    int DuplicatePairs,
    string ConfidenceNote);
