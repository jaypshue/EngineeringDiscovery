# EngineOS — Architecture & UI/UX Context

This steering file captures structural knowledge about the EngineOS web application
(project: EngineeringDiscovery) for use in future sessions.

---

## Technology Stack

- **Framework:** Blazor Server (.NET 10), Interactive Server render mode
- **Project:** `EngineeringDiscovery.Web` (csproj: `EngineOS.Web.csproj`)
- **CSS:** Global shell styles in `wwwroot/css/ed-shell.css`. Blazor scoped CSS via
  `*.razor.css` files. No Tailwind or CSS framework.
- **State:** Singleton `EngineeringDiscovery.Core.Services.WorkspaceState` (domain owner).
  Presentation mirror: `EngineeringDiscovery.Web.Services.WorkspaceStateService`.

---

## Solution Projects

| Project | Purpose |
|---|---|
| `EngineeringDiscovery.Core` | Domain logic, services, WorkspaceState, EngineeringPartner, ObservationEngine |
| `EngineeringDiscovery.Web` | Blazor Server web app (EngineOS UI) |
| `EngineeringDiscovery.Api` | ASP.NET API host |
| `EngineeringDiscovery.Desktop` | Desktop host |
| `EngineeringDiscovery.Wpf` | WPF host |
| `EngineeringDiscovery.Core.Tests` / `Web.Tests` / `Wpf.Tests` / `E2ETests` | Test projects |

---

## Web App Component Structure

```
Components/
  App.razor                  — HTML shell, loads global CSS, mounts Routes
  Routes.razor               — Router, DefaultLayout = MainLayout
  Layout/
    MainLayout.razor         — Primary app shell (ed-shell, ed-header, ed-body, ed-statusbar)
    MainLayout.razor.css     — Scoped CSS including EngineOS drawer styles
    AppLayout.razor          — Alternate shell used by /app route (different chrome)
    NavigationMenu.razor     — Side nav (shown only when !HasWorkspace)
  Pages/
    AppLanding.razor         — /app — shows ConversationExperience when HasWorkspace
    Dashboard.razor          — /dashboard — left RoleNavigation + WorkspaceHost
    ConversationExperience.razor — Conversation-first page component
    EngineeringWorkspace.razor  — /workspace
    LandingExperience.razor  — Public landing
    WelcomeWorkspace.razor   — /engineos
    ...and others
  Conversation/
    ConversationHost.razor   — Primary conversation UI (messages, composer, recommendations)
  Dashboard/
    WorkspaceHost.razor      — Repository import, view tabs (Explorer/Developer/Iterate),
                               renders InvestigationDashboard / workspace components
    InvestigationDashboard.razor — The Explorer (project/namespace/type tree)
    InvestigationDashboard.razor.cs — ViewModel build logic
    RoleNavigation.razor     — Role selector (CurrentTask, Architect, Planner, Developer, Verifier, Reviewer)
  Shared/
    ConversationArea.razor
    RecommendationCard.razor
    CurrentUnderstandingCard.razor
    ...
  Workspace/
    EngineeringContextCard.razor
    WorkContractEditor.razor
  Workspaces/
    InvestigationWorkspace, IterationWorkspace, RecommendationWorkspace, etc.
```

---

## Intended UI/UX Model

- **Conversation is the primary workflow.** `ConversationHost` is the entry surface.
  All other panels (Recommendation, CurrentUnderstanding, WorkContract) follow it.
- **Explorer is independent.** The `InvestigationDashboard` (Explorer) is a read-only
  structural view of the imported repository. It should visually fit the application
  without coupling to the conversation surface.
- **EngineOS drawer** is a collapsible companion panel that provides conversation access
  from any page. It is a layout-level concern, not a workspace concern.

---

## EngineOS Drawer — Layout Position (post-refactor)

The EngineOS drawer lives in **`MainLayout.razor`**, not in `WorkspaceHost`.

- **Collapsed:** compact bar anchored `left: 24px`, `top: 72px`, `pointer-events: none`
  except the bar itself. Does not obstruct content.
- **Expanded:** centred floating panel, `width: min(1100px, 78vw)`, `height: 68vh`,
  `z-index: 1200`, with a dim backdrop at `z-index: 1100`.
- **State:** `EngineOSExpanded` bool + `ConversationHost` ref owned by `MainLayout`.
- **CSS:** `MainLayout.razor.css` using `::deep` selectors for the fixed overlay elements.

Before this refactor, the drawer was embedded in `WorkspaceHost` as an inline `<style>`
block with `position: fixed`, which caused:
- A `padding-top: 136px` hack on `#workspace-main` to compensate for the overlay
- Unscoped global CSS injected on every render
- The Explorer being implicitly coupled to the drawer's dimensions

---

## Key Layout CSS Classes

| Class | Location | Purpose |
|---|---|---|
| `.ed-shell` | `ed-shell.css` | Full-height flex column app shell |
| `.ed-header` | `ed-shell.css` | 56px top header |
| `.ed-body` | `ed-shell.css` | flex row: nav + workspace |
| `.ed-nav` | `ed-shell.css` | 240px left nav (hidden when HasWorkspace) |
| `.ed-workspace` | `ed-shell.css` | flex:1, overflow:auto, main content area |
| `.ed-statusbar` | `ed-shell.css` | 36px bottom status bar |
| `.dashboard-grid` | `ed-shell.css` | flex row: left-pane (250px) + main-pane |
| `.engineos-drawer` | `MainLayout.razor.css` | EngineOS companion panel (fixed) |
| `.engineos-backdrop` | `MainLayout.razor.css` | Dim overlay behind expanded drawer |
| `.conversation-experience-shell` | `ed-shell.css` | max-width:1200px centered container |

---

## Routing

| Route | Component | Layout |
|---|---|---|
| `/` | `LandingExperience` | `MainLayout` |
| `/app` | `AppLanding` → `ConversationExperience` | `AppLayout` ⚠️ |
| `/dashboard` | `Dashboard` → `WorkspaceHost` | `MainLayout` |
| `/workspace` | `EngineeringWorkspace` | `MainLayout` |
| `/engineos` | `WelcomeWorkspace` | `MainLayout` |
| `/repository` | Repository import | `MainLayout` |

⚠️ `/app` uses `@layout AppLayout` which is a different shell from the rest of the app.
This is a known structural inconsistency (identified but not yet resolved).

---

## State Services

- `WorkspaceState` (Core, Singleton) — canonical domain state: active workspace,
  investigation, imported repositories, freshness, OnChange event.
- `WorkspaceStateService` (Web, Singleton) — presentation mirror: repo name/path,
  current goal/story/status, work contract fields.
- `WorkContractService` (Web, Scoped) — session-scoped work contract draft management.
- `SessionStartupService` (Web, Singleton) — coordinates focus/placeholder passing
  between LandingExperience and ConversationExperience on first load.
- `IRepositorySelectionService` / `RepositorySelectionService` (Web, Scoped) —
  handles repo path detection, validation, and client-side file picker summaries.

---

## Engineering Domain Concepts

- **Investigation** — result of running the discovery engine on a repository.
  Contains `TypeObservations`, `NamespaceObservations`, `MemberObservations`, `Findings`, `Artifacts`.
- **Workspace** — top-level container. Has `ImportedRepositories` (list) and a
  top-level `Investigation` (legacy/convenience field).
- **ImportedRepository** — per-repo discovery result within a Workspace.
- **WorkContract** — a structured engineering agreement (title, objective, status).
  Managed by `WorkContractService`. Rendered by `WorkContractEditor`.
- **EngineeringPartner** (`IEngineeringPartner`) — conversation session manager.
  Spike path: `ENGINEOS_SPIKE_LUNA=true` env var routes messages through `LunaConversationService`.
- **ObservationEngine** (`IObservationService`) — ingests typed observations
  (UserMessage, RepoAttached, DecisionAccepted, etc.) and updates engineering state.

---

## Known Issues / Technical Debt

1. `/app` uses `AppLayout` instead of `MainLayout` — visual disconnection from app chrome.
2. `max-width` conflict: `ed-shell.css` uses `!important` overrides to strip
   `ConversationHost`'s inline `max-width: 960px`. Ownership of width is unclear.
3. `ConversationArea.razor` references `ConversationComposer` which doesn't exist —
   produces `RZ10012` warning at build time (pre-existing).
4. Several `RZ10012` warnings in `Dashboard.razor` for `SectionHeader`, `RepositoryCard`,
   `StatusCard`, `ActionCard` — missing `@using` directives (pre-existing).
