# BACKLOG

## Discovery

### HIGH PRIORITY
- Exclude generated folders (bin/obj)


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

### ED-124: Package & Framework Discovery

Package versions, Eventually discover: Serilog 9.1.0, EF Core 10.0, OpenTelemetry 1.12
Still factual.

Central Package Management
Detect
Directory.Packages.props
Very common now.

NuGet.config
Worth knowing.

Local package feeds
Eventually.

SDK.props / SDK.targets
Eventually.

### ED-125
#### Graph Queries
Imagine the Architect asking, Which projects depend on Core? The graph answers instantly.

ex.Show every path from Web to Shared.
Graph.

Which projects become unreachable if Infrastructure disappears?
Graph.

Find all leaf projects.
Graph.

Find all root projects.
Graph.

### ED-166
Replace namespace-prefix framework detection with a richer reference/classification model when a concrete capability requires it.

## various Notes

- richer ProjectModel that contains them as strongly typed properties instead of emitting everything as standalone findings. The findings remain the communication mechanism, but the underlying model can become richer over time. That's an internal evolution and doesn't need to change ED-121.


- Report which providers were added.
- Parse appsettings contents.
- Environment hierarchy.
- Configuration binding.
- Options pattern.


