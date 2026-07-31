using EngineeringDiscovery.Core.Models;
using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.Investigation
{
    public class Investigation
    {
        private Investigation(Guid id, string repositoryPath)
        {
            Id = id;
            RepositoryPath = repositoryPath ?? throw new ArgumentNullException(nameof(repositoryPath));
            Status = InvestigationStatus.Created;
            Findings = new List<Finding>();
            Artifacts = new List<EngineeringDiscovery.Core.Models.InvestigationArtifact>();

            // Default engineering stage statuses
            ArchitectureStatus = EngineeringStageStatus.NotStarted;
            PlanningStatus = EngineeringStageStatus.NotStarted;
            DevelopmentStatus = EngineeringStageStatus.NotStarted;
            VerificationStatus = EngineeringStageStatus.NotStarted;
        }

        public Guid Id { get; private set; }

        public string RepositoryPath { get; private set; }

        public InvestigationStatus Status { get; private set; }

        public List<Finding> Findings { get; }

        // Owned artifacts: canonical collection for engineering knowledge artifacts
        public List<EngineeringDiscovery.Core.Models.InvestigationArtifact> Artifacts { get; }

        public DateTime? StartedAt { get; private set; }

        public DateTime? CompletedAt { get; private set; }

        // New domain properties
        public string Goal { get; private set; } = string.Empty;

        public string Owner { get; private set; } = string.Empty;

        public string Target { get; private set; } = string.Empty;

        // Engineering lifecycle stages
        public EngineeringStageStatus ArchitectureStatus { get; private set; }

        public EngineeringStageStatus PlanningStatus { get; private set; }

        public EngineeringStageStatus DevelopmentStatus { get; private set; }

        public EngineeringStageStatus VerificationStatus { get; private set; }

        // Observations (structured discovery facts) are owned by the Investigation aggregate.
        // Exposed as IReadOnlyList and mutable only via AddObservation to preserve encapsulation.
        private readonly List<DiscoveryObservation> _observations = new();
        public IReadOnlyList<DiscoveryObservation> Observations => _observations.AsReadOnly();

        // Structured member observations collected during discovery. These are mutable only by discovery code
        // and are exposed here for engineering rules to consume without string parsing.
        private readonly List<EngineeringDiscovery.Core.Models.MemberObservation> _memberObservations = new();
        public IReadOnlyList<EngineeringDiscovery.Core.Models.MemberObservation> MemberObservations => _memberObservations.AsReadOnly();

        // Structured type observations collected during discovery. Mutable only via discovery step.
        private readonly List<EngineeringDiscovery.Core.Models.TypeObservation> _typeObservations = new();
        public IReadOnlyList<EngineeringDiscovery.Core.Models.TypeObservation> TypeObservations => _typeObservations.AsReadOnly();

        // Structured namespace observations collected during discovery. Mutable only via discovery step.
        private readonly List<EngineeringDiscovery.Core.Models.NamespaceObservation> _namespaceObservations = new();
        public IReadOnlyList<EngineeringDiscovery.Core.Models.NamespaceObservation> NamespaceObservations => _namespaceObservations.AsReadOnly();

        // Structured project observation: canonical project-level metrics populated by enrichment passes
        private EngineeringDiscovery.Core.Models.ProjectObservation? _projectObservation;
        public EngineeringDiscovery.Core.Models.ProjectObservation? ProjectObservation => _projectObservation;

        // Set or update the ProjectObservation. Enrichment passes should call this to publish derived metrics.
        public void SetProjectObservation(EngineeringDiscovery.Core.Models.ProjectObservation projectObservation)
        {
            if (projectObservation is null) throw new ArgumentNullException(nameof(projectObservation));
            if (Status != InvestigationStatus.Started) throw new InvalidOperationException("ProjectObservation can only be set while the investigation is Started.");
            _projectObservation = projectObservation;
        }

        // Add a structured MemberObservation to the investigation.
        public void AddMemberObservation(EngineeringDiscovery.Core.Models.MemberObservation observation)
        {
            if (observation is null) throw new ArgumentNullException(nameof(observation));
            if (Status != InvestigationStatus.Started) throw new InvalidOperationException("Member observations can only be added while the investigation is Started.");
            _memberObservations.Add(observation);
        }

        // Add a structured TypeObservation to the investigation.
        public void AddTypeObservation(EngineeringDiscovery.Core.Models.TypeObservation observation)
        {
            if (observation is null) throw new ArgumentNullException(nameof(observation));
            if (Status != InvestigationStatus.Started) throw new InvalidOperationException("Type observations can only be added while the investigation is Started.");
            _typeObservations.Add(observation);
        }

        public static Investigation Create(Guid id, string repositoryPath)
            => Create(id, repositoryPath, goal: string.Empty, owner: string.Empty, target: string.Empty);

        public static Investigation Create(Guid id, string repositoryPath, string goal, string owner, string target,
            EngineeringStageStatus architectureStatus = EngineeringStageStatus.NotStarted,
            EngineeringStageStatus planningStatus = EngineeringStageStatus.NotStarted,
            EngineeringStageStatus developmentStatus = EngineeringStageStatus.NotStarted,
            EngineeringStageStatus verificationStatus = EngineeringStageStatus.NotStarted)
        {
            if (id == Guid.Empty) throw new ArgumentException("id must be provided", nameof(id));
            if (string.IsNullOrWhiteSpace(repositoryPath)) throw new ArgumentException("repositoryPath must be provided", nameof(repositoryPath));

            var inv = new Investigation(id, repositoryPath)
            {
                Goal = goal ?? string.Empty,
                Owner = owner ?? string.Empty,
                Target = target ?? string.Empty,
                ArchitectureStatus = architectureStatus,
                PlanningStatus = planningStatus,
                DevelopmentStatus = developmentStatus,
                VerificationStatus = verificationStatus
            };

            return inv;
        }

        public void Start()
        {
            if (Status != InvestigationStatus.Created)
                throw new InvalidOperationException("Investigation can only be started from Created state.");

            Status = InvestigationStatus.Started;
            StartedAt = DateTime.UtcNow;
        }

        public void Complete()
        {
            if (Status != InvestigationStatus.Started)
                throw new InvalidOperationException("Investigation can only be completed from Started state.");

            Status = InvestigationStatus.Completed;
            CompletedAt = DateTime.UtcNow;
        }

        public void Reopen()
        {
            if (Status != InvestigationStatus.Completed)
                throw new InvalidOperationException("Investigation can only be reopened from Completed state.");

            Status = InvestigationStatus.Started;
            CompletedAt = null;
        }

        public void AddFinding(Finding finding)
        {
            if (finding is null) throw new ArgumentNullException(nameof(finding));
            if (Status != InvestigationStatus.Started) throw new InvalidOperationException("Findings can only be added while the investigation is Started.");

            Findings.Add(finding);
        }

        // Add a DiscoveryObservation to the Investigation.
        public void AddObservation(DiscoveryObservation observation)
        {
            if (observation is null) throw new ArgumentNullException(nameof(observation));
            if (Status != InvestigationStatus.Started) throw new InvalidOperationException("Observations can only be added while the investigation is Started.");

            _observations.Add(observation);
        }

        // Allow updating stage statuses in a controlled way
        public void SetArchitectureStatus(EngineeringStageStatus status) => ArchitectureStatus = status;
        public void SetPlanningStatus(EngineeringStageStatus status) => PlanningStatus = status;
        public void SetDevelopmentStatus(EngineeringStageStatus status) => DevelopmentStatus = status;
        public void SetVerificationStatus(EngineeringStageStatus status) => VerificationStatus = status;
    }
}
