# BACKLOG

## Discovery

### Discovery Status
Replace string-based DiscoveryStatus with a strongly typed enum.

```csharp
public enum DiscoveryStatus
{
    Idle,
    Running,
    Complete,
    Failed
}
```

Reason:
- Eliminate string comparisons
- Improve compile-time safety
- Support future failure handling