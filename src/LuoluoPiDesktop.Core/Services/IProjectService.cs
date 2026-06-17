using LuoluoPiDesktop.Core.Models;

namespace LuoluoPiDesktop.Core.Services;

public interface IProjectService
{
    IReadOnlyList<ProjectEntry> Projects { get; }

    /// <summary>添加项目。目录不存在时抛 DirectoryNotFoundException。</summary>
    ProjectEntry Add(string localPath, string? name = null);

    /// <summary>重命名。</summary>
    void Rename(string id, string newName);

    /// <summary>从列表移除（不删除磁盘文件）。</summary>
    void Remove(string id);

    /// <summary>更新最后使用时间并保存。</summary>
    void Touch(string id);

    /// <summary>刷新单个项目的运行时状态（git、AGENTS.md）。</summary>
    void RefreshStatus(ProjectEntry entry);

    /// <summary>刷新所有项目的运行时状态。</summary>
    void RefreshAll();
}
