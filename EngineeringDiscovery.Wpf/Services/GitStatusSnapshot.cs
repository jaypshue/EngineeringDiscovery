using System;

namespace EngineeringDiscovery.Wpf.Services;

/// <summary>
/// Small, presentation-ready projection of the active repository's Git state.
/// </summary>
public sealed record GitStatusSnapshot(
    string RepositoryPath,
    bool IsRepository,
    string Branch,
    bool IsWorkingTreeClean,
    int StagedChanges,
    int ModifiedFiles,
    int UntrackedFiles,
    int DeletedFiles,
    string MostRecentCommit,
    string? ErrorMessage)
{
    public static GitStatusSnapshot Empty(string? errorMessage = null) => new(
        string.Empty,
        false,
        "—",
        true,
        0,
        0,
        0,
        0,
        "—",
        errorMessage);

    public string WorkingTreeState => !IsRepository
        ? "Unavailable"
        : IsWorkingTreeClean ? "Clean" : "Changes present";
}
