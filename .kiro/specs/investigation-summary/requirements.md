# Requirements Document

## Introduction

The Investigation Summary feature surfaces a structured, data-driven summary of an `Investigation`
to the engineer. It answers the question _"What did the discovery engine find in this repository?"_
with quantitative discovery metrics (project/namespace/type/member counts) and a categorised
breakdown of engineering artifacts.

The feature is split into two independently deliverable areas:

- **Core/API area** — a service interface (`IInvestigationSummaryService`) in `Core`, its
  `InvestigationSummaryService` implementation, and an HTTP endpoint
  `GET /api/investigation/summary` in `Api`.
- **UI area** — a new `InvestigationSummaryCard` Blazor component rendered above
  `InvestigationDashboard` inside `WorkspaceHost`'s Explorer tab, with event-driven refresh
  via `WorkspaceState.OnChange`.

The two areas share the existing `InvestigationSummary` domain value object as their contract;
no changes are made to that type's shape.

---

## Glossary

- **Investigation**: The result of running the discovery engine on a repository. Contains
  `TypeObservations`, `NamespaceObservations`, `MemberObservations`, `Findings`, and `Artifacts`.
- **InvestigationSummary**: An immutable value object derived from an `Investigation` using
  `InvestigationSummary.CreateFrom`. Contains discovery metrics and per-type artifact counts.
- **IInvestigationSummaryService**: The application-boundary interface in `Core.Services` that
  exposes summary derivation to API and other host consumers.
- **InvestigationSummaryService**: The concrete implementation of `IInvestigationSummaryService`
  that reads from `WorkspaceState` and delegates to `InvestigationSummary.CreateFrom`.
- **InvestigationSummaryController**: The ASP.NET `ApiController` that exposes the summary as
  `GET /api/investigation/summary`.
- **InvestigationSummaryCard**: The Blazor Server component that renders the summary as a card
  with discovery metrics and artifact groups.
- **WorkspaceHost**: The Blazor component at `/dashboard` that hosts repository import, view
  tabs (Explorer/Developer/Iterate), and renders `InvestigationDashboard`.
- **WorkspaceState**: The singleton domain-state service in `Core`. Fires `OnChange` whenever
  the active investigation changes.
- **ActiveWorkspace**: The currently loaded workspace exposed by `WorkspaceState`.
- **ErrorBoundary**: A Blazor built-in component that catches render-time exceptions and
  prevents them from propagating to the rest of the page.
- **Artifact**: A finding produced by the discovery engine, classified by `ArtifactType`
  (e.g., `LayerViolation`, `LongMethod`).

---

## Requirements

### Requirement 1: `InvestigationSummary` Domain Value Object Contract

**User Story:** As a Core library consumer, I want `InvestigationSummary.CreateFrom` to produce a reliable, non-negative, internally consistent snapshot of an investigation, so that any feature built on top of it can trust the values without additional validation.

#### Acceptance Criteria

1. WHEN `InvestigationSummary.CreateFrom` is called with a non-null `Investigation`, THE `InvestigationSummary` SHALL return a value object where every count field (`ProjectCount`, `NamespaceCount`, `TypeCount`, `MemberCount`, and all artifact-type counts) is greater than or equal to zero.

2. WHEN `InvestigationSummary.CreateFrom` is called with a non-null `Investigation`, THE `InvestigationSummary` SHALL set `TotalArtifacts` equal to the total number of artifact entries in `Investigation.Artifacts` (i.e., `Investigation.Artifacts.Count` or equivalent enumerable count), not necessarily equal to the sum of the individual named artifact-type counts if some artifact types are not enumerated individually.

3. WHEN `InvestigationSummary.CreateFrom` is called twice with the same `Investigation` instance, THE `InvestigationSummary` SHALL produce two value objects with identical values for every public property, and `Investigation.Artifacts.Count`, `Investigation.TypeObservations.Count`, `Investigation.NamespaceObservations.Count`, and `Investigation.MemberObservations.Count` SHALL each have the same values after both calls as before the first call.

4. IF `InvestigationSummary.CreateFrom` is called with a null argument, THEN THE `InvestigationSummary` SHALL throw `ArgumentNullException` and SHALL NOT return a value.

5. WHEN `InvestigationSummary.CreateFrom` is called with a non-null `Investigation` whose `RepositoryPath` yields a non-empty directory name, THE `InvestigationSummary` SHALL set `RepositoryName` to that directory name.

6. WHEN `InvestigationSummary.CreateFrom` is called with a non-null `Investigation` whose `RepositoryPath` does not yield a non-empty directory name (e.g., the path is a root path or empty), THE `InvestigationSummary` SHALL set `RepositoryName` to the full `RepositoryPath` string or to an empty string, and SHALL NOT return null for `RepositoryName`.

---

### Requirement 2: `IInvestigationSummaryService` Interface Contract

**User Story:** As an API or host consumer, I want a clearly defined, synchronous service contract for obtaining the current investigation summary, so that I can consume summary data without taking a direct dependency on `WorkspaceState`.

#### Acceptance Criteria

1. THE `IInvestigationSummaryService` SHALL expose a single synchronous `GetSummary()` method that returns `InvestigationSummary?` (nullable).

2. WHILE `WorkspaceState.ActiveWorkspace` is null, THE `IInvestigationSummaryService.GetSummary` SHALL return null and SHALL NOT throw an exception.

3. WHILE `WorkspaceState.ActiveWorkspace` is non-null and `ActiveWorkspace.Investigation` is null, THE `IInvestigationSummaryService.GetSummary` SHALL return null and SHALL NOT throw an exception.

4. WHEN `WorkspaceState.ActiveWorkspace` is non-null and `ActiveWorkspace.Investigation` is non-null, THE `IInvestigationSummaryService.GetSummary` SHALL return a value structurally equal to `InvestigationSummary.CreateFrom(ActiveWorkspace.Investigation)`, where structural equality means every public property of the returned `InvestigationSummary` has the same value as the corresponding property on the instance returned by `InvestigationSummary.CreateFrom(ActiveWorkspace.Investigation)` at the time of the call.

5. THE `IInvestigationSummaryService` SHALL be defined in the `EngineeringDiscovery.Core.Services` namespace so that consumers in `Api` and other host projects do not depend on Web-layer types.

6. IF `WorkspaceState.ActiveWorkspace` transitions from non-null to null between two successive calls to `GetSummary()`, THEN THE `IInvestigationSummaryService.GetSummary` SHALL return null on the subsequent call and SHALL NOT return a value from the previous workspace state.

7. THE `IInvestigationSummaryService` SHALL NOT expose any method, property, or event other than `GetSummary()`, so that the interface boundary remains minimal and stable.

---

### Requirement 3: `InvestigationSummaryService` Registration and Lifetime

**User Story:** As a host developer, I want `InvestigationSummaryService` registered with the correct DI lifetime, so that it shares the same `WorkspaceState` singleton instance as the rest of the application.

#### Acceptance Criteria

1. THE `InvestigationSummaryService` SHALL be registered in the DI container as `Singleton`, mapping `IInvestigationSummaryService` to `InvestigationSummaryService`.

2. THE `InvestigationSummaryService` SHALL accept `WorkspaceState` as a constructor parameter and SHALL NOT resolve `WorkspaceState` through a service locator, static accessor, or `IServiceProvider`.

3. IF `WorkspaceState` is not registered in the DI container at application startup, THEN the host SHALL throw an exception indicating a missing required dependency before the application begins serving requests.

---

### Requirement 4: `InvestigationSummaryController` HTTP Endpoint

**User Story:** As an external or desktop-host consumer, I want a stable HTTP endpoint that returns the current investigation summary, so that I can display or process discovery results without running a Blazor Server circuit.

#### Acceptance Criteria

1. THE `InvestigationSummaryController` SHALL expose an HTTP GET endpoint at the route `GET /api/investigation/summary`.

2. WHEN `IInvestigationSummaryService.GetSummary()` returns a non-null `InvestigationSummary`, THE `InvestigationSummaryController` SHALL return HTTP `200 OK` with the `InvestigationSummary` serialised as JSON in the response body and a `Content-Type: application/json` response header.

3. WHEN `IInvestigationSummaryService.GetSummary()` returns null, THE `InvestigationSummaryController` SHALL return HTTP `204 No Content` with no response body.

4. THE `InvestigationSummaryController` SHALL NOT contain business logic; all derivation logic SHALL remain in `IInvestigationSummaryService`.

5. IF `IInvestigationSummaryService.GetSummary()` throws an unexpected exception, THEN THE `InvestigationSummaryController` SHALL allow the exception to propagate to the ASP.NET error-handling middleware and SHALL NOT swallow the exception silently.

6. THE `InvestigationSummaryController` SHALL accept `IInvestigationSummaryService` via constructor injection and SHALL NOT resolve it through a service locator or static accessor.

7. WHEN a request is made to `GET /api/investigation/summary` using an HTTP method other than GET, THE `InvestigationSummaryController` SHALL return HTTP `405 Method Not Allowed`.

---

### Requirement 5: `InvestigationSummaryCard` Component — Display and Derivation

**User Story:** As an engineer using EngineOS, I want a summary card that shows discovery metrics and artifact findings for the current investigation, so that I can immediately understand what the discovery engine found in my repository.

#### Acceptance Criteria

1. WHEN the `Investigation` parameter of `InvestigationSummaryCard` is non-null, THE `InvestigationSummaryCard` SHALL derive an `InvestigationSummary` by calling `InvestigationSummary.CreateFrom(Investigation)` and SHALL render each of the following four discovery metrics as a labelled pair of metric name and numeric count: `ProjectCount` (labelled "Projects"), `NamespaceCount` (labelled "Namespaces"), `TypeCount` (labelled "Types"), and `MemberCount` (labelled "Members").

2. WHEN the `Investigation` parameter is non-null and `InvestigationSummary.TotalArtifacts` is greater than zero, THE `InvestigationSummaryCard` SHALL render artifact groups: an "Architectural" group containing `LayerViolations` and `CircularProjectReferences`, and a "Code Quality" group containing `EmptyControllers`, `LongMethods`, `ExcessiveParameterCount`, `LargeConstructors`, `AsyncNamingIssues`, `LargePublicSurfaceAreas`, `LargeTypes`, `LargeInterfaces`, `DeepInheritanceHierarchies`, `ExcessivePublicFields`, and `MixedResponsibilities`.

3. WHEN rendering artifact groups, THE `InvestigationSummaryCard` SHALL suppress any individual artifact-type item whose count is zero from the rendered output.

4. WHEN the `Investigation` parameter is null, THE `InvestigationSummaryCard` SHALL render an empty-state placeholder message and SHALL NOT render discovery metrics or artifact groups.

5. WHEN the `Investigation` parameter is non-null and `InvestigationSummary.TotalArtifacts` equals zero, THE `InvestigationSummaryCard` SHALL render discovery metrics and SHALL NOT render any artifact group section.

6. THE `InvestigationSummaryCard` SHALL NOT cache or store the `InvestigationSummary`; it SHALL NOT retain any previously derived summary between `OnParametersSet` calls.

7. WHEN `OnParametersSet` is called on `InvestigationSummaryCard`, THE `InvestigationSummaryCard` SHALL re-derive the `InvestigationSummary` from the current `Investigation` parameter value (or set it to null if the parameter is null).

8. THE `InvestigationSummaryCard` SHALL NOT use inline styles; all visual styling SHALL be applied through the component's scoped CSS file (`InvestigationSummaryCard.razor.css`) using CSS classes defined in or inherited from `ed-shell.css`.

---

### Requirement 6: `InvestigationSummaryCard` Component — Event-Driven Refresh

**User Story:** As an engineer using EngineOS, I want the summary card to update automatically whenever the active investigation changes, so that I always see current data without manually refreshing the page.

#### Acceptance Criteria

1. WHEN `WorkspaceState.OnChange` fires, THE `WorkspaceHost` SHALL call `StateHasChanged` and pass the current value of `WorkspaceState.ActiveWorkspace.Investigation` as the `Investigation` parameter to `InvestigationSummaryCard`.

2. THE `InvestigationSummaryCard` SHALL NOT subscribe directly to `WorkspaceState.OnChange`; it SHALL receive the updated `Investigation` solely through its `[Parameter]`.

3. WHEN `WorkspaceHost` receives a `WorkspaceState.OnChange` event while the Explorer tab is active, THE `InvestigationSummaryCard` SHALL re-render within one Blazor render cycle without requiring a full page reload.

4. WHEN `WorkspaceHost` is disposed, THE `WorkspaceHost` SHALL unsubscribe from `WorkspaceState.OnChange` to prevent further callbacks after disposal.

---

### Requirement 7: `WorkspaceHost` Explorer Tab Integration

**User Story:** As an engineer using EngineOS, I want the investigation summary card to appear above the repository explorer in the Explorer tab, so that I see high-level metrics before drilling into the type tree.

#### Acceptance Criteria

1. WHEN the Explorer tab is active in `WorkspaceHost`, THE `WorkspaceHost` SHALL render `InvestigationSummaryCard` in the DOM before the `InvestigationDashboard` element.

2. WHEN the Explorer tab is active and `WorkspaceState.ActiveWorkspace.Investigation` is non-null, THE `WorkspaceHost` SHALL pass that investigation as the `Investigation` parameter to `InvestigationSummaryCard`.

3. WHEN the Explorer tab is active and `WorkspaceState.ActiveWorkspace.Investigation` is null, THE `WorkspaceHost` SHALL pass null as the `Investigation` parameter to `InvestigationSummaryCard`, causing the card to display its empty-state placeholder.

4. THE `InvestigationSummaryCard` SHALL be rendered on the Explorer tab regardless of whether `InvestigationDashboard` is rendered; the two components SHALL NOT be coupled to each other's presence.

5. IF `WorkspaceState.ActiveWorkspace` is null, THEN THE `WorkspaceHost` SHALL pass null as the `Investigation` parameter to `InvestigationSummaryCard`.

---

### Requirement 8: Error Isolation

**User Story:** As an engineer using EngineOS, I want a rendering failure in the summary card to be isolated from the rest of the dashboard, so that the explorer tree and other workspace panels remain usable even if the card encounters an unexpected error.

#### Acceptance Criteria

1. THE `WorkspaceHost` SHALL wrap `InvestigationSummaryCard` in a Blazor `<ErrorBoundary>` component so that render-time exceptions thrown by the card are caught and do not propagate to the rest of the dashboard.

2. IF `InvestigationSummaryCard` throws an exception during rendering, THEN THE `ErrorBoundary` SHALL display an error message indicating that the summary is unavailable, while all sibling components including `InvestigationDashboard` remain visible and interactive.

3. IF `InvestigationSummaryCard` throws an exception during rendering, THEN THE `ErrorBoundary` SHALL preserve the last successfully rendered state of all sibling components without triggering a full page reload.
