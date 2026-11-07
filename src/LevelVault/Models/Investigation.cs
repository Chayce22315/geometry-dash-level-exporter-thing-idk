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
