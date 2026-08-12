# Implementation Plan: Investigation Summary

## Overview

The feature is split into two independently deliverable areas that can proceed in parallel:

- **Area 1 — Core/API**: service interface, implementation, DI registration, HTTP endpoint, and all
  non-UI tests (unit tests for the service and controller, property-based tests for `CreateFrom`).
- **Area 2 — UI**: `InvestigationSummaryCard` Blazor component, its scoped CSS, integration into
  `WorkspaceHost`, and bUnit integration tests.

Both areas share the existing `InvestigationSummary` domain type as their contract; no changes are
made to that type's shape.

---

## Tasks

### Area 1 — Core / API

- [ ] 1. Define `IInvestigationSummaryService` interface in Core
  - [ ] 1.1 Create `EngineeringDiscovery.Core/Services/IInvestigationSummaryService.cs`
    - Declare namespace `EngineeringDiscovery.Core.Services`
    - Expose a single synchronous method `InvestigationSummary? GetSummary()`
    - Add XML doc comment: returns null when no investigation is available; never throws
    - _Requirements: 2.1, 2.5, 2.7_

- [ ] 2. Implement `InvestigationSummaryService` in Core
  - [ ] 2.1 Create `EngineeringDiscovery.Core/Services/InvestigationSummaryService.cs`
    - Declare `sealed class InvestigationSummaryService : IInvestigationSummaryService`
    - Accept `WorkspaceState` via constructor; store in `readonly` field
    - Implement `GetSummary()`: return null when `ActiveWorkspace == null` or
      `ActiveWorkspace.Investigation == null`; otherwise delegate to
      `InvestigationSummary.CreateFrom(investigation)`
    - _Requirements: 2.2, 2.3, 2.4, 2.6_

  - [ ]* 2.2 Write unit tests for `InvestigationSummaryService`
    - Add test class `InvestigationSummaryServiceTests` in `EngineeringDiscovery.Core.Tests`
    - Test: `GetSummary()` returns null when `ActiveWorkspace` is null
    - Test: `GetSummary()` returns null when `ActiveWorkspace.Investigation` is null
    - Test: `GetSummary()` returns a value structurally equal to `CreateFrom(investigation)`
      when both workspace and investigation are non-null
    - Use the existing `WorkspaceState` class directly (construct with registered
      `InMemoryWorkspacePersistence` via `ServiceCollection` as in `EntryRoutingTests`)
    - _Requirements: 2.2, 2.3, 2.4_

- [ ] 3. Register `InvestigationSummaryService` in the Api DI container
  - [ ] 3.1 Update `EngineeringDiscovery.Api/Program.cs`
    - Add `builder.Services.AddSingleton<IInvestigationSummaryService, InvestigationSummaryService>()`
      after `WorkspaceState` is registered (verify ordering so `WorkspaceState` is available)
    - Add required `using EngineeringDiscovery.Core.Services;` if not already present
    - _Requirements: 3.1, 3.2, 3.3_

- [ ] 4. Add `InvestigationSummaryController` to the Api project
  - [ ] 4.1 Create directory `EngineeringDiscovery.Api/Controllers/` and add
    `InvestigationSummaryController.cs`
    - Decorate with `[ApiController]` and `[Route("api/investigation")]`
    - Accept `IInvestigationSummaryService` via constructor injection
    - Implement `[HttpGet("summary")]` action `GetSummary()` returning
      `ActionResult<InvestigationSummary>`:
      - Call `_service.GetSummary()`
      - Return `NoContent()` (204) when null
      - Return `Ok(summary)` (200) when non-null
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7_

  - [ ]* 4.2 Write unit tests for `InvestigationSummaryController`
    - Add test class `InvestigationSummaryControllerTests` in `EngineeringDiscovery.Core.Tests`
      (or a new `Api.Tests` project if one exists; otherwise use `Core.Tests` with an
      `Api` project reference)
    - Create a minimal stub/fake `IInvestigationSummaryService` returning null or a value
    - Test: controller returns `204 NoContent` when `GetSummary()` returns null
    - Test: controller returns `200 OK` with the correct `InvestigationSummary` body when
      `GetSummary()` returns non-null
    - _Requirements: 4.2, 4.3_

- [ ] 5. Add property-based tests for `InvestigationSummary.CreateFrom`
  - [ ] 5.1 Add `CsCheck` NuGet package to `EngineeringDiscovery.Core.Tests.csproj`
    - Use pinned version: `CsCheck` `3.12.0` (or latest stable; verify on nuget.org before
      adding)
    - CsCheck is a pure-C# property-testing library; no F# runtime dependency
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [ ]* 5.2 Write property test — non-negativity (Property 2)
    - **Property 2: Non-negativity**
    - **Validates: Requirements 1.1**
    - Generate random valid `Investigation` instances using CsCheck generators
    - Assert: every count field (`ProjectCount`, `NamespaceCount`, `TypeCount`, `MemberCount`,
      and all artifact-type counts) is `>= 0`

  - [ ]* 5.3 Write property test — TotalArtifacts source (Property 2 — consistency clause)
    - **Property 2: Consistency — TotalArtifacts equals Artifacts collection count**
    - **Validates: Requirements 1.2**
    - Assert: `TotalArtifacts == Investigation.Artifacts.Count` (or equivalent enumerable
      count) for any valid investigation

  - [ ]* 5.4 Write property test — idempotency (Property 2 — idempotency clause)
    - **Property 2: Idempotency**
    - **Validates: Requirements 1.3**
    - Call `CreateFrom(investigation)` twice on the same instance
    - Assert: all public properties on both results are equal
    - Assert: the investigation's collection counts are unchanged after both calls

  - [ ]* 5.5 Write unit test — null guard (Property 2 — null guard clause)
    - **Property 2: Null guard**
    - **Validates: Requirements 1.4**
    - Assert: `CreateFrom(null)` throws `ArgumentNullException`
    - (Use a standard xUnit `Assert.Throws` — no generator needed)

- [ ] 6. Area 1 checkpoint
  - Build `EngineeringDiscovery.Core` and `EngineeringDiscovery.Api` — both must compile clean
  - Run all tests in `EngineeringDiscovery.Core.Tests` — all must pass
  - Ensure all tests pass; ask the user if questions arise.

---

### Area 2 — UI

- [ ] 7. Create `InvestigationSummaryCard` Blazor component
  - [ ] 7.1 Create `EngineeringDiscovery.Web/Components/Dashboard/InvestigationSummaryCard.razor`
    - Add `@namespace EngineeringDiscovery.Web.Components.Dashboard`
    - Add `@using EngineeringDiscovery.Core.Domain.Investigation`
    - Declare `[Parameter] public Investigation? Investigation { get; set; }`
    - Implement `OnParametersSet`: when `Investigation` is null set `_summary = null`,
      `_discoveryItems = []`, `_artifactGroups = []`; when non-null call
      `InvestigationSummary.CreateFrom(Investigation)` and build `_discoveryItems` (4 entries)
      and `_artifactGroups` (Architectural / Code Quality, suppressing zero-count items)
    - Render: empty-state `<div class="inv-summary-card inv-summary-card--empty">` with
      placeholder text when `_summary` is null
    - Render: `<section class="inv-summary-card">` with header (`RepositoryName`), metrics
      grid, and artifact groups when `_summary` is non-null
    - Do not call `StateHasChanged()` inside `OnParametersSet`; do not subscribe to
      `WorkspaceState.OnChange` — refreshes arrive via parameter from `WorkspaceHost`
    - Use only CSS classes; no inline styles
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8, 6.2_

  - [ ] 7.2 Create `EngineeringDiscovery.Web/Components/Dashboard/InvestigationSummaryCard.razor.css`
    - Define `.inv-summary-card`, `.inv-summary-card--empty`, `.inv-summary-header`,
      `.inv-summary-repo`, `.inv-summary-metrics`, `.inv-summary-metric`,
      `.inv-metric-label`, `.inv-metric-value`, `.inv-summary-artifacts`,
      `.inv-artifacts-heading`, `.inv-artifact-group`, `.inv-artifact-group-name`,
      `.inv-artifact-list`, `.inv-artifact-count`
    - Use CSS custom properties from `ed-shell.css` (e.g. `var(--ed-color-*)`,
      `var(--ed-space-*)`) wherever colours and spacing are referenced
    - No `position: fixed` or inline-style overrides; respect the `.ed-workspace` scroll
      container
    - _Requirements: 5.8_

- [ ] 8. Integrate `InvestigationSummaryCard` into `WorkspaceHost`
  - [ ] 8.1 Update `EngineeringDiscovery.Web/Components/Dashboard/WorkspaceHost.razor`
    - In the Explorer tab render block, add before `<InvestigationDashboard>`:
      ```razor
      <ErrorBoundary>
          <ChildContent>
              <InvestigationSummaryCard Investigation="@(WorkspaceState.ActiveWorkspace?.Investigation)" />
          </ChildContent>
          <ErrorContent>
              <div class="inv-summary-error">Investigation summary unavailable.</div>
          </ErrorContent>
      </ErrorBoundary>
      ```
    - The card is rendered regardless of whether `InvestigationDashboard` is rendered (the
      existing null/empty-collection guard for the dashboard must not gate the card)
    - No new `WorkspaceState.OnChange` subscription needed — the existing subscription
      already calls `StateHasChanged()` which re-passes the parameter
    - Verify that `WorkspaceHost` unsubscribes from `WorkspaceState.OnChange` in `Dispose()`
      (add `IDisposable` implementation if not already present)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5, 8.1, 8.2, 8.3, 6.1, 6.3, 6.4_

- [ ] 9. Add bUnit integration tests for `InvestigationSummaryCard`
  - [ ] 9.1 Add `bUnit` NuGet package to the `EngineeringDiscovery.Web.Tests` project
    - Pinned version: `bunit` `1.28.9` (or latest stable; verify on nuget.org before adding)
    - Add a `EngineeringDiscovery.Web.Tests.csproj` project file if it does not already have
      one, referencing `EngineeringDiscovery.Web` and `xunit`
    - _Requirements: 5.1, 5.4, 6.3_

  - [ ]* 9.2 Write bUnit test — null investigation renders empty state
    - Add test class `InvestigationSummaryCardTests` in `EngineeringDiscovery.Web.Tests`
    - Render `<InvestigationSummaryCard Investigation="null" />`
    - Assert: output contains `.inv-summary-card--empty`
    - Assert: output does not contain `.inv-summary-metrics` or `.inv-summary-artifacts`
    - _Requirements: 5.4_

  - [ ]* 9.3 Write bUnit test — non-null investigation renders metrics
    - Construct a minimal `Investigation` with known project/namespace/type/member counts
    - Render `<InvestigationSummaryCard Investigation="inv" />`
    - Assert: output contains exactly four metric entries with the correct labels
      ("Projects", "Namespaces", "Types", "Members") and corresponding counts
    - Assert: `.inv-summary-card--empty` is not present
    - _Requirements: 5.1, 5.2, 5.3, 5.5_

  - [ ]* 9.4 Write bUnit test — OnChange triggers re-render with updated counts
    - Render the card with an initial investigation
    - Mutate the `Investigation` parameter to a new instance with different counts
    - Call `SetParametersAndRender` to simulate the parameter update from `WorkspaceHost`
    - Assert: the rendered output reflects the new counts, not the original ones
    - _Requirements: 6.3, 5.7_

- [ ] 10. Area 2 checkpoint
  - Build `EngineeringDiscovery.Web` — must compile clean
  - Run all tests in `EngineeringDiscovery.Web.Tests` — all must pass
  - Ensure all tests pass; ask the user if questions arise.

---

## Notes

- Tasks marked with `*` are optional and can be skipped for a faster MVP delivery.
- Areas 1 and 2 are independent; they may be executed in parallel or sequentially in any order.
- The `InvestigationSummaryCard` never calls the HTTP API — it reads `WorkspaceState` in-process via
  the `Investigation` parameter passed by `WorkspaceHost`. The API endpoint is the boundary for
  non-Blazor consumers only.
- Property tests in task 5 target `InvestigationSummary.CreateFrom` (the existing domain factory).
  If the factory already satisfies all properties, the tests simply confirm it — no behaviour change
  is expected.
- For the Api controller tests (task 4.2), if no `Api.Tests` project exists, add the
  `EngineeringDiscovery.Api` project reference to `Core.Tests` and place the tests there. Create a
  dedicated `Api.Tests` project only if the solution already follows that pattern.
- `CsCheck` was chosen over `FsCheck` because it has no F# runtime dependency, matching the
  all-C# ecosystem of this solution.
- Authentication/authorisation on `GET /api/investigation/summary` is out of scope for this feature
  but must be applied before production deployment.

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1"] },
    { "id": 1, "tasks": ["2.1", "5.1", "7.1"] },
    { "id": 2, "tasks": ["2.2", "3.1", "5.2", "5.3", "5.4", "5.5", "7.2"] },
    { "id": 3, "tasks": ["4.1", "9.1"] },
    { "id": 4, "tasks": ["4.2", "8.1", "9.2", "9.3", "9.4"] }
  ]
}
```
