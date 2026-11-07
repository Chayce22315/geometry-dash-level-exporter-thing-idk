using System.Collections.ObjectModel;

namespace LevelVault.Models;

public sealed class Investigation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Query { get; set; } = "";
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    public ObservableCollection<Clue> Clues { get; set; } = [];
    public ObservableCollection<EvidenceNode> EvidenceNodes { get; set; } = [];
    public ObservableCollection<EvidenceEdge> EvidenceEdges { get; set; } = [];
    public List<RecoveryCandidate> RecoveryCandidates { get; set; } = [];

    // Deep-research state is persisted locally so a run can resume without repeating completed queries.
    public List<string> CompletedResearchQueries { get; set; } = [];
    public List<ResearchQueryState> PendingResearchQueries { get; set; } = [];
    public string ResearchStatus { get; set; } = "not started";
    public int ResearchQueriesExecuted { get; set; }
    public int ResearchCluesAdded { get; set; }
    public DateTime? LastResearchStartedUtc { get; set; }
    public DateTime? LastResearchCompletedUtc { get; set; }
}

public sealed class RecoveryCandidate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string LocalPath { get; set; } = "";
    public string Format { get; set; } = "";
    public string LevelName { get; set; } = "";
    public string LevelId { get; set; } = "";
    public string Status { get; set; } = "unverified";
    public string ValidationSummary { get; set; } = "";
    public string RawFileSha256 { get; set; } = "";
    public string PayloadSha256 { get; set; } = "";
    public long PayloadBytes { get; set; }
    public DateTime DiscoveredUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RetrievedUtc { get; set; }
}

public sealed class Clue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Kind { get; set; } = "reference";
    public string Title { get; set; } = "";
    public string Detail { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Confidence { get; set; } = "unverified";
    public DateTime AddedUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ResearchQueryState
{
    public string Text { get; set; } = "";
    public int Depth { get; set; }
    public string Reason { get; set; } = "";
}
