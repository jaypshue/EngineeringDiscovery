# Known Limitations

## RelationshipGraph Does Not Persist Across Restart

**Date identified:** 2026-08-21

**Component:** `EngineeringDiscovery.Core.Investigation.Investigation.RelationshipGraph`

**Issue:**

The `RepositoryRelationshipGraph` is stored in a private backing field (`_relationshipGraph`) on the `Investigation` aggregate. The public property is read-only:

```csharp
private RepositoryRelationshipGraph? _relationshipGraph;
public RepositoryRelationshipGraph? RelationshipGraph => _relationshipGraph;
```

`FileWorkspacePersistence` uses `System.Text.Json` to serialize the `Workspace` object directly. `System.Text.Json` cannot serialize/deserialize private fields or readonly collection-backed properties by default. As a result:

- **During the active session:** The graph is fully populated and available (the `/graph` route works correctly).
- **After EngineOS restart:** The workspace is loaded from disk, but `RelationshipGraph` deserializes as `null`. The graph is lost until the repository is re-imported or the investigation is re-run.

**Affected data:**
- `Investigation.RelationshipGraph` (inheritance, dependency, implementation edges)
- `Investigation.TypeObservations` (IReadOnlyList backed by private List)
- `Investigation.NamespaceObservations` (same pattern)
- `Investigation.MemberObservations` (same pattern)
- `Investigation.Artifacts` (public List, but may not round-trip correctly)

**Impact:**
- The `/graph` architectural view is empty after restart
- `InvestigationSummary.CreateFrom()` returns zero counts after restart
- `EngineeringStateQuery.GetWorkspaceContext()` reports zero type/namespace/member counts after restart
- The post-import conversation summary cannot be regenerated from persisted state

**Workaround (current):**
- The investigation summary is captured as a conversation message during import, so the user sees it once. But subsequent sessions lose the structured investigation data.

**Recommended fix (separate engineering task):**
- Option A: Add `[JsonInclude]` attributes and public setters (or init-only setters) to the Investigation aggregate's private backing fields. Requires careful consideration of the domain model's encapsulation guarantees.
- Option B: Implement a dedicated `InvestigationDto` serialization layer (similar to the existing `WorkspaceDto` pattern that was started but not completed). Map Investigation → InvestigationDto for persistence and back on load.
- Option C: Serialize the Investigation separately (not as part of the Workspace JSON) using a custom serializer that handles the private fields.

**Priority:** High. The architectural model is a core EngineOS capability. It must survive a restart to be a real product feature. Without persistence, the user must re-import after every restart to regain architectural understanding.

**Scope:** This is an engineering task for the persistence layer, not the investigation pipeline or UI.
