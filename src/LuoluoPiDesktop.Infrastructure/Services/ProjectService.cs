using System.Diagnostics;
using System.IO;
using LuoluoPiDesktop.Core.Models;
using LuoluoPiDesktop.Core.Services;

namespace LuoluoPiDesktop.Infrastructure.Services;

public sealed class ProjectService : IProjectService
{
    private readonly ISettingsService _settings;
    private readonly IAppLogger       _logger;

    public IReadOnlyList<ProjectEntry> Projects => _settings.Current.Projects;

    public ProjectService(ISettingsService settings, IAppLogger logger)
    {
        _settings = settings;
        _logger   = logger;
    }

    public ProjectEntry Add(string localPath, string? name = null)
    {
        localPath = Path.GetFullPath(localPath.Trim());

        if (!Directory.Exists(localPath))
            throw new DirectoryNotFoundException($"目录不存在：{localPath}");

        if (_settings.Current.Projects.Any(p =>
                string.Equals(p.LocalPath, localPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"该目录已添加：{localPath}");

        var entry = new ProjectEntry
        {
            Name       = name ?? GuessName(localPath),
            LocalPath  = localPath,
            LastUsedAt = DateTime.Now,
        };

        RefreshStatus(entry);
        _settings.Current.Projects.Add(entry);
        _settings.Save();
        _logger.Info($"Project added: {localPath}");
        return entry;
    }

    public void Rename(string id, string newName)
    {
        var entry = Find(id);
        if (string.IsNullOrWhiteSpace(newName)) return;
        entry.Name = newName.Trim();
        _settings.Save();
        _logger.Info($"Project renamed: {id} → {newName}");
    }

    public void Remove(string id)
    {
        var entry = Find(id);
        _settings.Current.Projects.Remove(entry);
        _settings.Save();
        _logger.Info($"Project removed: {entry.LocalPath}");
    }

    public void Touch(string id)
    {
        var entry = Find(id);
        entry.LastUsedAt = DateTime.Now;
        _settings.Save();
    }

    public void RefreshStatus(ProjectEntry entry)
    {
        entry.PathExists  = Directory.Exists(entry.LocalPath);
        entry.IsGitRepo   = entry.PathExists && Directory.Exists(Path.Combine(entry.LocalPath, ".git"));
        entry.HasAgentsMd = entry.PathExists &&
            (File.Exists(Path.Combine(entry.LocalPath, "AGENTS.md")) ||
             File.Exists(Path.Combine(entry.LocalPath, "agents.md")));
        entry.GitBranch   = entry.IsGitRepo ? ReadGitBranch(entry.LocalPath) : string.Empty;
    }

    public void RefreshAll()
    {
        foreach (var p in _settings.Current.Projects)
            RefreshStatus(p);
    }

    // ── helpers ──────────────────────────────────────────────────────────

    private ProjectEntry Find(string id)
        => _settings.Current.Projects.FirstOrDefault(p => p.Id == id)
           ?? throw new KeyNotFoundException($"Project not found: {id}");

    private static string GuessName(string path)
    {
        var name = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar,
                                                  Path.AltDirectorySeparatorChar));
        return string.IsNullOrEmpty(name) ? path : name;
    }

    private string ReadGitBranch(string repoPath)
    {
        // Read .git/HEAD directly — no git.exe dependency
        var headFile = Path.Combine(repoPath, ".git", "HEAD");
        if (!File.Exists(headFile)) return string.Empty;
        try
        {
            var head = File.ReadAllText(headFile).Trim();
            // "ref: refs/heads/main" → "main"
            if (head.StartsWith("ref: refs/heads/"))
                return head["ref: refs/heads/".Length..];
            // detached HEAD — return short sha
            return head.Length >= 7 ? head[..7] : head;
        }
        catch (Exception ex)
        {
            _logger.Warn($"ReadGitBranch failed for {repoPath}: {ex.Message}");
            return string.Empty;
        }
    }
}
