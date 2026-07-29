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




## various Notes

richer ProjectModel that contains them as strongly typed properties instead of emitting everything as standalone findings. The findings remain the communication mechanism, but the underlying model can become richer over time. That's an internal evolution and doesn't need to change ED-121.