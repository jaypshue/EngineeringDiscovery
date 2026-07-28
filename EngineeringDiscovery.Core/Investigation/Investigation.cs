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
        }

        public Guid Id { get; private set; }

        public string RepositoryPath { get; private set; }

        public InvestigationStatus Status { get; private set; }

        public List<Finding> Findings { get; }

        public DateTime? StartedAt { get; private set; }

        public DateTime? CompletedAt { get; private set; }

        public static Investigation Create(Guid id, string repositoryPath)
        {
            if (id == Guid.Empty) throw new ArgumentException("id must be provided", nameof(id));
            if (string.IsNullOrWhiteSpace(repositoryPath)) throw new ArgumentException("repositoryPath must be provided", nameof(repositoryPath));

            return new Investigation(id, repositoryPath);
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
    }
}
