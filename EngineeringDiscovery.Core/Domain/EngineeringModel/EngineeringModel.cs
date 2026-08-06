using System;
using System.Collections.Generic;

namespace EngineeringDiscovery.Core.Domain.EngineeringModel
{
    public enum EngineeringStatus
    {
        Initializing,
        Discovering,
        EngineeringModelReady,
        Archived
    }

    public enum DiscoveryStatus
    {
        Unknown,
        Partial,
        Complete
    }

    public sealed class DiscoveryCategory
    {
        public DiscoveryCategory()
        {
            Name = string.Empty;
            Status = DiscoveryStatus.Unknown;
            Confidence = 0.0;
            SupportingFacts = new List<EngineeringFact>();
            ExpectedQuestion = string.Empty;
        }

        public string Name { get; set; }

        public DiscoveryStatus Status { get; set; }

        public double Confidence { get; set; }

        public List<EngineeringFact> SupportingFacts { get; }

        // Optional mapping to the seeded question that would primarily satisfy this category
        public string ExpectedQuestion { get; set; }
    }

    public enum DiscoveryState
    {
        Discovering,
        Ready
    }

    public enum ObjectiveStatus
    {
        NotStarted,
        Active,
        Complete,
        Deferred
    }

    public enum ObjectiveType
    {
        Product,
        Engineering
    }

    public sealed class DiscoveryObjective
    {
        public DiscoveryObjective()
        {
            Id = Guid.NewGuid();
            Name = string.Empty;
            Status = ObjectiveStatus.NotStarted;
            IsRequired = false;
            RequiredFacts = new List<string>();
            CollectedFacts = new List<EngineeringFact>();
            Type = ObjectiveType.Product;
            LastAskedFact = string.Empty;
        }

        public Guid Id { get; set; }
        public string Name { get; set; }
        public ObjectiveStatus Status { get; set; }
        public bool IsRequired { get; set; }
        public List<string> RequiredFacts { get; set; }
        public List<EngineeringFact> CollectedFacts { get; set; }
        public ObjectiveType Type { get; set; }
        // Last fact the orchestrator asked for this objective (for correlating answers)
        public string LastAskedFact { get; set; }
    }

    public sealed class EngineeringFact
    {
        public EngineeringFact()
        {
            Id = Guid.NewGuid();
            Key = string.Empty;
            Value = string.Empty;
        }

        public Guid Id { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public sealed class EngineeringQuestion
    {
        public EngineeringQuestion()
        {
            Id = Guid.NewGuid();
            Question = string.Empty;
            Reason = string.Empty;
            Priority = 0;
            Objective = string.Empty;
            TargetCategory = string.Empty;
        }

        public Guid Id { get; set; }
        public string Question { get; set; }
        public string Reason { get; set; }
        public int Priority { get; set; }
        // The engineering objective this question intends to satisfy (e.g., "Clarify deployment constraints")
        public string Objective { get; set; }
        // Optional: the discovery category this question targets (e.g., "Deployment", "Architecture")
        public string TargetCategory { get; set; }
    }

    public sealed class ConversationEntry
    {
        public ConversationEntry()
        {
            Id = Guid.NewGuid();
            Speaker = string.Empty;
            Message = string.Empty;
            TimestampUtc = DateTime.UtcNow;
        }

        public Guid Id { get; set; }
        public string Speaker { get; set; }
        public string Message { get; set; }
        public DateTime TimestampUtc { get; set; }
    }

    public sealed class EngineeringModel
    {
        public EngineeringModel()
        {
            Id = Guid.NewGuid();
            OriginalIdea = string.Empty;
            Status = EngineeringStatus.Initializing;
            Confidence = 0.0;
            OverallDiscoveryReadiness = 0.0;
            CurrentFocus = string.Empty;
            DiscoveryObjectives = new List<DiscoveryObjective>();
        }

        public Guid Id { get; init; }

        public string OriginalIdea { get; set; }

        public EngineeringStatus Status { get; set; }

        public double Confidence { get; set; }

        public List<EngineeringFact> KnownFacts { get; } = new();

        public List<EngineeringQuestion> OpenQuestions { get; } = new();

        public List<Domain.Activity.EngineeringDecision> Decisions { get; } = new();

        public List<ConversationEntry> Conversation { get; } = new();

        // Discovery categories projection representing areas of engineering knowledge
        public List<DiscoveryCategory> DiscoveryCategories { get; } = new();

        // Computed overall readiness (0.0 - 100.0)
        public double OverallDiscoveryReadiness { get; set; }

        // A short human-readable description of what EngineOS is currently focused on
        public string CurrentFocus { get; set; }

        // Explicit discovery objectives owned by the orchestrator
        public List<DiscoveryObjective> DiscoveryObjectives { get; }
    }
}
