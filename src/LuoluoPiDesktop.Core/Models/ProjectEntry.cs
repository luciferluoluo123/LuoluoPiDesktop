namespace LuoluoPiDesktop.Core.Models;

public sealed class ProjectEntry
{
    public string   Id           { get; set; } = Guid.NewGuid().ToString();
    public string   Name         { get; set; } = string.Empty;
    public string   LocalPath    { get; set; } = string.Empty;
    public string   DefaultModel { get; set; } = string.Empty;
    public bool     IsEnabled    { get; set; } = true;
    public DateTime LastUsedAt   { get; set; } = DateTime.MinValue;

    // --- Phase 2 新增 ---

    // 运行时状态（不持久化，每次启动重新扫描）
    public bool   PathExists      { get; set; }
    public bool   IsGitRepo       { get; set; }
    public bool   HasAgentsMd     { get; set; }
    public string GitBranch       { get; set; } = string.Empty;
}
