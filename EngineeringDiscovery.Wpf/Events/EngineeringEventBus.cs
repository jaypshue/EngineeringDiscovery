using System;

namespace EngineeringDiscovery.Wpf.Events
{
    public enum EngineeringEventType
    {
        ConversationUpdated,
        PackageGenerated,
        PackageApproved,
        ImplementationReceived,
        EvidenceCollected
    }

    public class EngineeringEvent
    {
        public EngineeringEventType Type { get; }
        public object? Payload { get; }
        public DateTime TimestampUtc { get; }

        public EngineeringEvent(EngineeringEventType type, object? payload = null)
        {
            Type = type;
            Payload = payload;
            TimestampUtc = DateTime.UtcNow;
        }
    }

    // Lightweight in-process event bus for engineering events.
    public static class EngineeringEventBus
    {
        private static event Action<EngineeringEvent>? _eventPublished;

        public static void Publish(EngineeringEvent evt)
        {
            try
            {
                _eventPublished?.Invoke(evt);
            }
            catch
            {
                // swallow exceptions from subscribers to keep publisher robust
            }
        }

        public static void Subscribe(Action<EngineeringEvent> handler)
        {
            _eventPublished += handler;
        }

        public static void Unsubscribe(Action<EngineeringEvent> handler)
        {
            _eventPublished -= handler;
        }
    }
}
