namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    /// <summary>
    /// Read-only projection of workspace-level context (repository/investigation status).
    /// Used by EngineeringPartner to make engineering decisions without accessing WorkspaceState directly.
    /// </summary>
    public sealed class WorkspaceContext
    {
        public bool HasRepository { get; init; }
        public string RepositoryName { get; init; } = string.Empty;
        public string RepositoryPath { get; init; } = string.Empty;
        public bool HasInvestigation { get; init; }
        public int DiscoveredTypeCount { get; init; }
        public int DiscoveredNamespaceCount { get; init; }
        public int DiscoveredMemberCount { get; init; }
    }
}
