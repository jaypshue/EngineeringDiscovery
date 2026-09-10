using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Wpf.Models;

public sealed class RepositoryFileNode
{
    public RepositoryFileNode(string name, string fullPath, string relativePath, bool isDirectory)
    {
        Name = name;
        FullPath = fullPath;
        RelativePath = relativePath;
        IsDirectory = isDirectory;
    }

    public string Name { get; }
    public string FullPath { get; }
    public string RelativePath { get; }
    public bool IsDirectory { get; }
    public IList<RepositoryFileNode> Children { get; } = new List<RepositoryFileNode>();
    public string DisplayName => IsDirectory ? $"{Name}/" : Name;
}

public sealed record FileSearchResult(string RelativePath, string FullPath, int? LineNumber, int? ColumnNumber, string? Preview)
{
    public string Location => LineNumber is null ? RelativePath : $"{RelativePath}:{LineNumber}:{ColumnNumber ?? 1}";
}

public enum GitChangeKind
{
    Modified,
    Added,
    Deleted,
    Renamed,
    Untracked,
    TypeChanged,
    Unknown
}

public sealed record GitChange(
    string RelativePath,
    string FullPath,
    GitChangeKind Kind,
    bool IsStaged,
    bool IsUntracked,
    bool IsDeleted,
    string StatusCode)
{
    public string DisplayStatus => IsUntracked ? "U" : StatusCode.Trim();
    public string DisplayName => $"{DisplayStatus,-2} {RelativePath}";
}

public enum ProblemSeverity
{
    Error,
    Warning,
    Info
}

public sealed record ProblemItem(
    ProblemSeverity Severity,
    string Message,
    string? FilePath = null,
    int? Line = null,
    int? Column = null,
    string? Code = null,
    string? Source = null)
{
    public string DisplayLocation => string.IsNullOrWhiteSpace(FilePath)
        ? string.Empty
        : $"{FilePath}{(Line is null ? string.Empty : $":{Line}:{Column ?? 1}")}";
}

public enum DevelopmentOutputChannel
{
    Build,
    Test,
    Git,
    Search,
    File,
    System
}

public sealed record DevelopmentOutputEntry(
    DateTimeOffset Timestamp,
    DevelopmentOutputChannel Channel,
    string Text,
    bool IsError = false)
{
    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Text}";
}

public enum DevelopmentCommandKind
{
    Build,
    Test
}

public sealed record DevelopmentCommandResult(
    DevelopmentCommandKind Kind,
    string Command,
    int ExitCode,
    TimeSpan Duration,
    IReadOnlyList<ProblemItem> Problems,
    string Output)
{
    public bool Succeeded => ExitCode == 0;
    public string DisplayStatus => Succeeded ? "Passed" : "Failed";
}

public sealed record DevelopmentResultViewModel(
    string Name,
    string Status,
    string Summary,
    DateTimeOffset CompletedAt,
    int ProblemCount)
{
    public string DisplayText => $"{Name}: {Status} — {Summary}";
}
