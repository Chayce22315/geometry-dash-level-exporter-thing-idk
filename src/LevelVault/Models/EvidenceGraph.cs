namespace LevelVault.Models;

public sealed class EvidenceNode
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "reference";
    public string Label { get; set; } = "";
    public string SourceUrl { get; set; } = "";
    public string Notes { get; set; } = "";
}

public sealed class EvidenceEdge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public string Relationship { get; set; } = "references";
    public string Rationale { get; set; } = "";
    public string Confidence { get; set; } = "unverified";
}
