using System;

namespace EngineeringDiscovery.Core.Domain.ProjectState
{
    public enum LifecyclePhase
    {
        Inception,
        ActiveDevelopment,
        Stabilization,
        Maintenance,
        Paused,
        Archived
    }

    /// <summary>
    /// Captures current lifecycle phase and engineering focus.
    /// Answers "where is this project in its lifecycle."
    /// </summary>
    public sealed class ProjectLifecycle
    {
        public ProjectLifecycle()
        {
            Phase = LifecyclePhase.Inception;
            CurrentFocus = string.Empty;
            PhaseEnteredUtc = DateTime.UtcNow;
            LastActivityUtc = PhaseEnteredUtc;
        }

        public LifecyclePhase Phase { get; set; }
        public string CurrentFocus { get; set; }
        public DateTime PhaseEnteredUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
    }
}
