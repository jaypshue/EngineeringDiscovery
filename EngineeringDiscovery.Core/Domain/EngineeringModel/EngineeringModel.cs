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
        Coaching,
        Ready
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
        }

        public Guid Id { get; set; }
        public string Question { get; set; }
        public string Reason { get; set; }
        public int Priority { get; set; }
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
    }
}
