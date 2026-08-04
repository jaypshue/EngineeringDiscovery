using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Domain.CurrentTask;
using EngineeringDiscovery.Core.Domain.Workspace;
using Microsoft.Extensions.Logging;

namespace EngineeringDiscovery.Core.Services
{
    public sealed class CurrentTaskService : ICurrentTaskService
    {
        private readonly WorkspaceState _state;
        private readonly IWorkspacePersistence _persistence;
        private readonly ITimeProvider _time;
        private readonly ILogger<CurrentTaskService>? _logger;
        private readonly object _lock = new object();

        public CurrentTaskService(WorkspaceState state, IWorkspacePersistence persistence, ITimeProvider time, ILogger<CurrentTaskService>? logger = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _logger = logger;
        }

        public Task BeginTaskAsync(string title, string description, string goal)
        {
            if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title required", nameof(title));

            lock (_lock)
            {
                var ws = _state.ActiveWorkspace ?? new Workspace();
                var now = _time.UtcNow;
                var task = new CurrentTask(title, description, goal);
                // set timestamps using the provided time provider
                // CurrentTask ctor sets CreatedUtc/UpdatedUtc already
                ws.CurrentTask = task;
                _state.ReplaceWorkspace(ws);
                // Persist canonical workspace via persistence supplied to service
                try { _persistence.SaveAsync(ws).GetAwaiter().GetResult(); } catch { }
            }

            return Task.CompletedTask;
        }

        public Task CompleteTaskAsync()
        {
            lock (_lock)
            {
                var ws = _state.ActiveWorkspace ?? throw new InvalidOperationException("No active workspace");
                if (ws.CurrentTask is null) throw new InvalidOperationException("No current task to complete");
                ws.CurrentTask.Complete();
                // Clear current task after completion policy
                ws.CurrentTask = null;
                ws.Touch();
                _state.ReplaceWorkspace(ws);
                try { _persistence.SaveAsync(ws).GetAwaiter().GetResult(); } catch { }
            }

            return Task.CompletedTask;
        }

        public Task UpdateBriefAsync(Action<EngineeringBrief> update)
        {
            if (update is null) throw new ArgumentNullException(nameof(update));

            lock (_lock)
            {
                var ws = _state.ActiveWorkspace ?? throw new InvalidOperationException("No active workspace");
                var ct = ws.CurrentTask ?? throw new InvalidOperationException("No current task to update");
                update(ct.Brief);
                ct.Brief.Touch();
                ws.Touch();
                _state.ReplaceWorkspace(ws);
                try { _persistence.SaveAsync(ws).GetAwaiter().GetResult(); } catch { }
            }

            return Task.CompletedTask;
        }

        public Task AddContextAsync(string kind, string id)
        {
            if (string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("kind", nameof(kind));
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id", nameof(id));

            lock (_lock)
            {
                var ws = _state.ActiveWorkspace ?? throw new InvalidOperationException("No active workspace");
                var ct = ws.CurrentTask ?? throw new InvalidOperationException("No current task to update");
                switch (kind)
                {
                    case "Project": ct.Brief.Context.AddProject(id); break;
                    case "Namespace": ct.Brief.Context.AddNamespace(id); break;
                    case "Type": ct.Brief.Context.AddType(id); break;
                    default: break;
                }
                ct.Brief.Touch();
                ws.Touch();
                _state.ReplaceWorkspace(ws);
                try { _persistence.SaveAsync(ws).GetAwaiter().GetResult(); } catch { }
            }

            return Task.CompletedTask;
        }
    }
}
