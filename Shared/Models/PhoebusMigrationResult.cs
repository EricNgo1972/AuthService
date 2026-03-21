namespace AuthService.Shared.Models;

public sealed class PhoebusMigrationResult
{
    public bool DryRun { get; init; }
    public int SourceRows { get; set; }
    public int ImportedTenants { get; set; }
    public int ImportedUsers { get; set; }
    public int ImportedMemberships { get; set; }
    public int ExistingTenants { get; set; }
    public int ExistingUsers { get; set; }
    public int ExistingMemberships { get; set; }
    public int SkippedRows { get; set; }
    public int ErrorCount => Issues.Count;
    public List<string> Issues { get; } = [];
}
