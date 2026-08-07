using System;

namespace EngineeringDiscovery.Wpf.Models
{
    public enum EvidenceCategory
    {
        Repository,
        Conversation,
        Architecture,
        Build,
        Tests,
        Screenshots,
        Implementation,
        PackageReview
    }

    public class EngineeringEvidence
    {
        public EvidenceCategory Category { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }
}
