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



## ED-....

### ED-118.1 (Backlog) - Support both solution formats.

Supported Investigation Targets

✓ *.sln
✓ *.slnx

Everywhere.

file picker
validation
discovery engine
documentation

Internally, "Solution" should mean:
