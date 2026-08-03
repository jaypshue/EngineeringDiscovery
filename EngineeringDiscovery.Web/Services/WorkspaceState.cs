using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using EngineeringDiscovery.Core.Domain.Workspace;
using Microsoft.Extensions.Logging;

namespace EngineeringDiscovery.Web.Services
{
    public sealed class WorkspaceState
    {
        public enum EngineeringModelFreshness
        {
            Unknown,
            Current,
            RefreshRecommended,
            RefreshRequired
        }

        private const string AppFolderName = "EngineeringDiscovery";
        private const string WorkspaceFileName = "workspace.json";

        private readonly string _workspaceFilePath;
        private readonly ILogger<WorkspaceState>? _logger;

        public WorkspaceState(ILogger<WorkspaceState>? logger = null)
        {
            _logger = logger;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appFolder = Path.Combine(localAppData, AppFolderName);
            if (!Directory.Exists(appFolder)) Directory.CreateDirectory(appFolder);

            _workspaceFilePath = Path.Combine(appFolder, WorkspaceFileName);

            ActiveWorkspace = LoadWorkspace();
        }

        // Compute a simple repository fingerprint: the latest LastWriteTimeUtc among top-level files and solution files.
        // This is intentionally lightweight for V1 and can be replaced with git commit, hashing, or indexing later.
        public string? ComputeRepositoryFingerprint(string repositoryPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repositoryPath)) return null;
                if (File.Exists(repositoryPath))
                {
                    var fi = new FileInfo(repositoryPath);
                    return fi.LastWriteTimeUtc.ToString("o");
                }

                if (!Directory.Exists(repositoryPath)) return null;

                // Consider top-level files + any solution files in tree
                var topFiles = Directory.EnumerateFiles(repositoryPath, "*.*", SearchOption.TopDirectoryOnly);
                var solFiles = Directory.EnumerateFiles(repositoryPath, "*.sln*", SearchOption.AllDirectories);

                var fileTimes = topFiles.Concat(solFiles)
                    .Select(p => File.GetLastWriteTimeUtc(p));

                if (!fileTimes.Any()) return null;

                var latest = fileTimes.Max();
                return latest.ToString("o");
            }
            catch
            {
                return null;
            }
        }

        public Workspace? ActiveWorkspace { get; private set; }

        public bool HasWorkspace => ActiveWorkspace is not null && !ActiveWorkspace.IsEmpty();

        public event Action? OnChange;

        public void ImportRepository(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath)) throw new ArgumentException("repositoryPath is required", nameof(repositoryPath));

            var workspace = new Workspace
            {
                RepositoryPath = repositoryPath.Trim(),
                Investigation = null,
                CurrentTask = null
            };

            ActiveWorkspace = workspace;
            Save();
            NotifyStateChanged();

            // Discovery pipeline should be invoked here to populate Investigation.
            // ImportRepository currently creates the persisted workspace record; callers should populate Investigation
            // and then call ReplaceWorkspace(workspace) to persist a fully initialized workspace.
        }

        public void Save()
        {
            try
            {
                if (ActiveWorkspace is null) return;

                ActiveWorkspace.Touch();

                var dto = ToDto(ActiveWorkspace);
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(dto, options);
                File.WriteAllText(_workspaceFilePath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save workspace to {Path}", _workspaceFilePath);
            }
        }

        private Workspace? LoadWorkspace()
        {
            try
            {
                if (!File.Exists(_workspaceFilePath)) return null;

                var json = File.ReadAllText(_workspaceFilePath);

                // Detect schema version. If present and valid, deserialize via DTO. Otherwise attempt migration from legacy shape.
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // If schema version exists, prefer a tolerant migration path that avoids System.Text.Json deserialization
                // which can throw on unexpected token types (for example numeric enum values). The migration routine
                // extracts required fields robustly from the JsonElement and returns a WorkspaceDto.
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("SchemaVersion", out _))
                {
                    var dtoFromElement = MigrateDtoFromElement(root);
                    if (dtoFromElement is null) return null;

                    try
                    {
                        // Persist migrated DTO to ensure future loads use the DTO shape
                        var migratedJsonFromElement = JsonSerializer.Serialize(dtoFromElement, new JsonSerializerOptions { WriteIndented = true });
                        File.WriteAllText(_workspaceFilePath, migratedJsonFromElement);
                    }
                    catch (Exception mex)
                    {
                        _logger?.LogWarning(mex, "Failed to persist migrated workspace DTO; continuing with in-memory migration.");
                    }

                    return FromDto(dtoFromElement);
                }

                // Legacy workspace.json detected (no SchemaVersion). Migrate by extracting the smallest useful subset into the DTO shape.
                _logger?.LogInformation("Migrating legacy workspace.json to current schema.");

                var legacy = root;
                var dtoMig = new EngineeringDiscovery.Web.Services.Persistence.WorkspaceDto();

                // Id
                if (legacy.TryGetProperty("Id", out var idProp) && idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out var gid))
                    dtoMig.Id = gid;
                else
                    dtoMig.Id = Guid.NewGuid();

                dtoMig.SchemaVersion = "0"; // legacy migration marker

                // RepositoryPath
                if (legacy.TryGetProperty("RepositoryPath", out var repoProp) && repoProp.ValueKind == JsonValueKind.String)
                    dtoMig.RepositoryPath = repoProp.GetString() ?? string.Empty;

                // Timestamps
                if (legacy.TryGetProperty("CreatedUtc", out var createdProp) && createdProp.ValueKind == JsonValueKind.String && DateTime.TryParse(createdProp.GetString(), out var created))
                    dtoMig.CreatedUtc = created;
                else
                    dtoMig.CreatedUtc = DateTime.UtcNow;

                if (legacy.TryGetProperty("LastModifiedUtc", out var modProp) && modProp.ValueKind == JsonValueKind.String && DateTime.TryParse(modProp.GetString(), out var modified))
                    dtoMig.LastModifiedUtc = modified;
                else
                    dtoMig.LastModifiedUtc = dtoMig.CreatedUtc;

                // CurrentTask migration
                if (legacy.TryGetProperty("CurrentTask", out var ctProp) && ctProp.ValueKind == JsonValueKind.Object)
                {
                    var ct = new EngineeringDiscovery.Web.Services.Persistence.CurrentTaskDto();
                    if (ctProp.TryGetProperty("Title", out var t) && t.ValueKind == JsonValueKind.String) ct.Title = t.GetString() ?? string.Empty;
                    if (ctProp.TryGetProperty("Description", out var d) && d.ValueKind == JsonValueKind.String) ct.Description = d.GetString() ?? string.Empty;
                    if (ctProp.TryGetProperty("Goal", out var g) && g.ValueKind == JsonValueKind.String) ct.Goal = g.GetString() ?? string.Empty;
                    if (ctProp.TryGetProperty("Status", out var s) && s.ValueKind == JsonValueKind.String) ct.Status = s.GetString() ?? string.Empty;

                    // Brief migration if present
                    if (ctProp.TryGetProperty("Brief", out var b) && b.ValueKind == JsonValueKind.Object)
                    {
                        var brief = new EngineeringDiscovery.Web.Services.Persistence.EngineeringBriefDto();
                        if (b.TryGetProperty("Objective", out var bo) && bo.ValueKind == JsonValueKind.String) brief.Objective = bo.GetString() ?? string.Empty;
                        if (b.TryGetProperty("Notes", out var bn) && bn.ValueKind == JsonValueKind.String) brief.Notes = bn.GetString() ?? string.Empty;
                        if (b.TryGetProperty("ImplementationThoughts", out var bi) && bi.ValueKind == JsonValueKind.String) brief.ImplementationThoughts = bi.GetString() ?? string.Empty;
                        if (b.TryGetProperty("LastUpdatedUtc", out var bu) && bu.ValueKind == JsonValueKind.String && DateTime.TryParse(bu.GetString(), out var buDt)) brief.LastUpdatedUtc = buDt;
                        ct.Brief = brief;
                    }

                    dtoMig.CurrentTask = ct;
                }

                // Investigation migration: capture minimal metadata if available
                if (legacy.TryGetProperty("Investigation", out var invProp) && invProp.ValueKind == JsonValueKind.Object)
                {
                    var inv = new EngineeringDiscovery.Web.Services.Persistence.InvestigationDto();
                    if (invProp.TryGetProperty("Id", out var iid) && iid.ValueKind == JsonValueKind.String && Guid.TryParse(iid.GetString(), out var iidg)) inv.Id = iidg;
                    if (invProp.TryGetProperty("RepositoryPath", out var ir) && ir.ValueKind == JsonValueKind.String) inv.RepositoryPath = ir.GetString() ?? string.Empty;
                    if (invProp.TryGetProperty("Target", out var it) && it.ValueKind == JsonValueKind.String) inv.Target = it.GetString() ?? string.Empty;
                    if (invProp.TryGetProperty("Goal", out var ig) && ig.ValueKind == JsonValueKind.String) inv.Goal = ig.GetString() ?? string.Empty;
                    if (invProp.TryGetProperty("Owner", out var io) && io.ValueKind == JsonValueKind.String) inv.Owner = io.GetString() ?? string.Empty;
                    if (invProp.TryGetProperty("Status", out var isv) && isv.ValueKind == JsonValueKind.String) inv.Status = isv.GetString() ?? string.Empty;
                    dtoMig.Investigation = inv;
                }

                // Freshness: try to find LastBuiltUtc/RepositoryFingerprint in legacy JSON if present
                if (legacy.TryGetProperty("LastBuiltUtc", out var lb) && lb.ValueKind == JsonValueKind.String && DateTime.TryParse(lb.GetString(), out var lbDt)) dtoMig.LastBuiltUtc = lbDt;
                if (legacy.TryGetProperty("RepositoryFingerprint", out var rf) && rf.ValueKind == JsonValueKind.String) dtoMig.RepositoryFingerprint = rf.GetString();

                // Persist migrated DTO to new schema to avoid future migration
                var migratedLegacyJson = JsonSerializer.Serialize(dtoMig, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_workspaceFilePath, migratedLegacyJson);

                return FromDto(dtoMig);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load or migrate workspace from {Path}", _workspaceFilePath);
                return null;
            }
        }

        private EngineeringDiscovery.Web.Services.Persistence.WorkspaceDto? MigrateDtoFromElement(JsonElement element)
        {
            try
            {
                var dto = new EngineeringDiscovery.Web.Services.Persistence.WorkspaceDto();

                if (element.TryGetProperty("Id", out var idProp) && idProp.ValueKind == JsonValueKind.String && Guid.TryParse(idProp.GetString(), out var gid)) dto.Id = gid;
                else dto.Id = Guid.NewGuid();

                if (element.TryGetProperty("SchemaVersion", out var sv) && sv.ValueKind == JsonValueKind.String) dto.SchemaVersion = sv.GetString() ?? dto.SchemaVersion;

                if (element.TryGetProperty("RepositoryPath", out var rp) && rp.ValueKind == JsonValueKind.String) dto.RepositoryPath = rp.GetString() ?? string.Empty;

                if (element.TryGetProperty("SelectedRole", out var sr) && sr.ValueKind == JsonValueKind.String) dto.SelectedRole = sr.GetString();

                if (element.TryGetProperty("CreatedUtc", out var cu) && cu.ValueKind == JsonValueKind.String && DateTime.TryParse(cu.GetString(), out var cdt)) dto.CreatedUtc = cdt;
                if (element.TryGetProperty("LastModifiedUtc", out var lu) && lu.ValueKind == JsonValueKind.String && DateTime.TryParse(lu.GetString(), out var ldt)) dto.LastModifiedUtc = ldt;

                if (element.TryGetProperty("LastBuiltUtc", out var lb) && lb.ValueKind == JsonValueKind.String && DateTime.TryParse(lb.GetString(), out var lbudt)) dto.LastBuiltUtc = lbudt;
                if (element.TryGetProperty("RepositoryFingerprint", out var rf) && rf.ValueKind == JsonValueKind.String) dto.RepositoryFingerprint = rf.GetString();

                if (element.TryGetProperty("CurrentTask", out var ct) && ct.ValueKind == JsonValueKind.Object)
                {
                    var ctd = new EngineeringDiscovery.Web.Services.Persistence.CurrentTaskDto();
                    if (ct.TryGetProperty("Title", out var t) && t.ValueKind == JsonValueKind.String) ctd.Title = t.GetString() ?? string.Empty;
                    if (ct.TryGetProperty("Description", out var d) && d.ValueKind == JsonValueKind.String) ctd.Description = d.GetString() ?? string.Empty;
                    if (ct.TryGetProperty("Goal", out var g) && g.ValueKind == JsonValueKind.String) ctd.Goal = g.GetString() ?? string.Empty;
                    if (ct.TryGetProperty("Status", out var s) && s.ValueKind == JsonValueKind.String) ctd.Status = s.GetString() ?? string.Empty;

                    if (ct.TryGetProperty("Brief", out var b) && b.ValueKind == JsonValueKind.Object)
                    {
                        var bd = new EngineeringDiscovery.Web.Services.Persistence.EngineeringBriefDto();
                        if (b.TryGetProperty("Objective", out var bo) && bo.ValueKind == JsonValueKind.String) bd.Objective = bo.GetString() ?? string.Empty;
                        if (b.TryGetProperty("Notes", out var bn) && bn.ValueKind == JsonValueKind.String) bd.Notes = bn.GetString() ?? string.Empty;
                        if (b.TryGetProperty("ImplementationThoughts", out var bi) && bi.ValueKind == JsonValueKind.String) bd.ImplementationThoughts = bi.GetString() ?? string.Empty;
                        if (b.TryGetProperty("LastUpdatedUtc", out var bu) && bu.ValueKind == JsonValueKind.String && DateTime.TryParse(bu.GetString(), out var budt)) bd.LastUpdatedUtc = budt;
                        ctd.Brief = bd;
                    }

                    dto.CurrentTask = ctd;
                }

                if (element.TryGetProperty("Investigation", out var inv) && inv.ValueKind == JsonValueKind.Object)
                {
                    var idto = new EngineeringDiscovery.Web.Services.Persistence.InvestigationDto();
                    if (inv.TryGetProperty("Id", out var iid) && iid.ValueKind == JsonValueKind.String && Guid.TryParse(iid.GetString(), out var iidg)) idto.Id = iidg;
                    if (inv.TryGetProperty("RepositoryPath", out var ir) && ir.ValueKind == JsonValueKind.String) idto.RepositoryPath = ir.GetString() ?? string.Empty;
                    if (inv.TryGetProperty("Target", out var it) && it.ValueKind == JsonValueKind.String) idto.Target = it.GetString() ?? string.Empty;
                    if (inv.TryGetProperty("Goal", out var ig) && ig.ValueKind == JsonValueKind.String) idto.Goal = ig.GetString() ?? string.Empty;
                    if (inv.TryGetProperty("Owner", out var io) && io.ValueKind == JsonValueKind.String) idto.Owner = io.GetString() ?? string.Empty;
                    if (inv.TryGetProperty("Status", out var isv) && isv.ValueKind == JsonValueKind.String) idto.Status = isv.GetString() ?? string.Empty;
                    dto.Investigation = idto;
                }

                return dto;
            }
            catch
            {
                return null;
            }
        }

        public void ReplaceWorkspace(Workspace newWorkspace)
        {
            ActiveWorkspace = newWorkspace ?? throw new ArgumentNullException(nameof(newWorkspace));
            // When replacing (post-discovery), compute and record freshness metadata
            try
            {
                var fingerprint = ComputeRepositoryFingerprint(newWorkspace.RepositoryPath);
                ActiveWorkspace.SetFreshness(DateTime.UtcNow, fingerprint);
            }
            catch { }

            Save();
            NotifyStateChanged();
        }

        public EngineeringModelFreshness GetFreshnessStatus()
        {
            if (ActiveWorkspace is null) return EngineeringModelFreshness.Unknown;

            // No last build recorded
            if (!ActiveWorkspace.LastBuiltUtc.HasValue) return EngineeringModelFreshness.Unknown;

            var currentFingerprint = ComputeRepositoryFingerprint(ActiveWorkspace.RepositoryPath);
            if (string.IsNullOrWhiteSpace(ActiveWorkspace.RepositoryFingerprint) || string.IsNullOrWhiteSpace(currentFingerprint))
            {
                return EngineeringModelFreshness.Unknown;
            }

            if (string.Equals(ActiveWorkspace.RepositoryFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
            {
                return EngineeringModelFreshness.Current;
            }

            // Fingerprints differ: repository has changed since last build
            // For v1, treat any difference as RefreshRecommended (not Required) to avoid forcing immediate action.
            return EngineeringModelFreshness.RefreshRecommended;
        }

        private EngineeringDiscovery.Web.Services.Persistence.WorkspaceDto ToDto(Workspace workspace)
        {
            var dto = new EngineeringDiscovery.Web.Services.Persistence.WorkspaceDto
            {
                Id = workspace.Id,
                SchemaVersion = workspace.SchemaVersion,
                RepositoryPath = workspace.RepositoryPath,
                SelectedRole = workspace.SelectedRole.ToString(),
                CreatedUtc = workspace.CreatedUtc,
                LastModifiedUtc = workspace.LastModifiedUtc
            };

            if (workspace.CurrentTask is not null)
            {
                dto.CurrentTask = new EngineeringDiscovery.Web.Services.Persistence.CurrentTaskDto
                {
                    Title = workspace.CurrentTask.Title,
                    Description = workspace.CurrentTask.Description,
                    Goal = workspace.CurrentTask.Goal,
                    Status = workspace.CurrentTask.Status.ToString(),
                    Brief = new EngineeringDiscovery.Web.Services.Persistence.EngineeringBriefDto
                    {
                        Objective = workspace.CurrentTask.Brief.Objective,
                        Notes = workspace.CurrentTask.Brief.Notes,
                        ImplementationThoughts = workspace.CurrentTask.Brief.ImplementationThoughts,
                        LastUpdatedUtc = workspace.CurrentTask.Brief.LastUpdatedUtc
                    }
                };
            }

            if (workspace.Investigation is not null)
            {
                dto.Investigation = new EngineeringDiscovery.Web.Services.Persistence.InvestigationDto
                {
                    Id = workspace.Investigation.Id,
                    RepositoryPath = workspace.Investigation.RepositoryPath,
                    Target = workspace.Investigation.Target,
                    Goal = workspace.Investigation.Goal,
                    Owner = workspace.Investigation.Owner,
                    Status = workspace.Investigation.Status.ToString()
                };
            }

            return dto;
        }

        private Workspace FromDto(EngineeringDiscovery.Web.Services.Persistence.WorkspaceDto dto)
        {
            var workspace = new Workspace
            {
                Id = dto.Id,
                SchemaVersion = dto.SchemaVersion,
                RepositoryPath = dto.RepositoryPath,
                SelectedRole = Enum.TryParse<global::EngineeringDiscovery.Core.Domain.Models.EngineeringRole>(dto.SelectedRole ?? string.Empty, out var role) ? role : global::EngineeringDiscovery.Core.Domain.Models.EngineeringRole.CurrentTask
            };

            if (dto.CurrentTask is not null)
            {
                var ct = new global::EngineeringDiscovery.Core.Domain.CurrentTask.CurrentTask(dto.CurrentTask.Title, dto.CurrentTask.Description, dto.CurrentTask.Goal);
                // Set brief values
                ct.Brief.Objective = dto.CurrentTask.Brief?.Objective ?? string.Empty;
                ct.Brief.Notes = dto.CurrentTask.Brief?.Notes ?? string.Empty;
                ct.Brief.ImplementationThoughts = dto.CurrentTask.Brief?.ImplementationThoughts ?? string.Empty;
                workspace.CurrentTask = ct;
            }

            if (dto.Investigation is not null)
            {
                // Create a minimal Investigation aggregate using available persisted fields via the factory
                var inv = global::EngineeringDiscovery.Web.Services.InvestigationFactory.Create(dto.Investigation.RepositoryPath, dto.Investigation.Target, dto.Investigation.Goal, dto.Investigation.Owner);
                workspace.Investigation = inv;
            }

            // Restore freshness metadata if present
            if (dto.LastBuiltUtc.HasValue || !string.IsNullOrWhiteSpace(dto.RepositoryFingerprint))
            {
                workspace.SetFreshness(dto.LastBuiltUtc ?? DateTime.MinValue, dto.RepositoryFingerprint);
            }

            return workspace;
        }

        // Subscribe to external state changes so the Workspace can be persisted when underlying components mutate.
        public void RegisterPersistenceHooks(EngineeringDiscovery.Web.Services.CurrentTaskState currentTaskState, EngineeringDiscovery.Web.Services.InvestigationState investigationState)
        {
            // When compatibility state changes, copy the latest values into the active Workspace and persist.
            if (currentTaskState is not null)
            {
                currentTaskState.OnChange += () =>
                {
                    try
                    {
                        if (ActiveWorkspace is null) return;
                        ActiveWorkspace.CurrentTask = currentTaskState.ActiveTask;
                        Save();
                    }
                    catch { }
                };
            }

            if (investigationState is not null)
            {
                investigationState.OnChange += () =>
                {
                    try
                    {
                        if (ActiveWorkspace is null) return;
                        ActiveWorkspace.Investigation = investigationState.Investigation;
                        Save();
                    }
                    catch { }
                };
            }
        }

        private void NotifyStateChanged() => OnChange?.Invoke();
    }
}
