using System;
using System.Linq;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.Iteration;
using EngineeringDiscovery.Core.Domain.Workspace;

namespace EngineeringDiscovery.Web.Services
{
    // Lightweight presentation service to manage iterations associated with the active Workspace.
    public class IterationService
    {
        private readonly EngineeringDiscovery.Core.Services.WorkspaceState _workspaceState;

        public IterationService(EngineeringDiscovery.Core.Services.WorkspaceState workspaceState)
        {
            _workspaceState = workspaceState ?? throw new ArgumentNullException(nameof(workspaceState));
        }

        public EngineeringIteration? GetCurrentIteration()
        {
            var ws = _workspaceState.ActiveWorkspace;
            if (ws == null) return null;
            var it = ws.Iterations?.LastOrDefault(i => i.Status == IterationStatus.InProgress);
            return it;
        }

        public EngineeringIteration StartIteration(string goal)
        {
            var ws = _workspaceState.ActiveWorkspace ?? new Workspace();
            var it = new EngineeringIteration { Goal = goal ?? string.Empty };
            ws.Iterations ??= new System.Collections.Generic.List<EngineeringIteration>();
            ws.Iterations.Add(it);
            _workspaceState.ReplaceWorkspace(ws);
            _workspaceState.Save();
            return it;
        }

        public void AddStep(EngineeringIteration stepParent, EngineeringIterationStep step)
        {
            if (stepParent == null) throw new ArgumentNullException(nameof(stepParent));
            stepParent.Steps.Add(step);
            _workspaceState.Save();
        }

        public void CompleteIteration(EngineeringIteration it)
        {
            if (it == null) return;
            it.Status = IterationStatus.Completed;
            _workspaceState.Save();
        }

        public System.Collections.Generic.IReadOnlyList<EngineeringIteration> GetHistory()
        {
            var ws = _workspaceState.ActiveWorkspace;
            if (ws == null) return Array.Empty<EngineeringIteration>();
            return ws.Iterations ?? new System.Collections.Generic.List<EngineeringIteration>();
        }
    }
}
