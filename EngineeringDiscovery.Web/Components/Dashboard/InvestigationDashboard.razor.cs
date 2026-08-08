using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Web.Components.Dashboard.ViewModels;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Components.Dashboard
{
    public partial class InvestigationDashboard : ComponentBase
    {
        [Parameter]
        public Investigation? Investigation { get; set; }

        [Inject]
        private EngineeringDiscovery.Web.Services.InvestigationState InvestigationState { get; set; } = null!;

        protected EngineeringDiscovery.Web.Components.Dashboard.ViewModels.InvestigationDashboardViewModel? ViewModel { get; set; }

        protected string SearchTerm { get; set; } = string.Empty;

        protected HashSet<string> ExpandedProjects { get; } = new();
        protected HashSet<string> ExpandedNamespaces { get; } = new();
        protected HashSet<string> ExpandedTypes { get; } = new();

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            // If parent did not supply an Investigation parameter, use the shared InvestigationState.
            if (Investigation is null)
            {
                Investigation = InvestigationState.Investigation;
            }
            BuildViewModel();
        }

        protected void BuildViewModel()
        {
            ViewModel = new EngineeringDiscovery.Web.Components.Dashboard.ViewModels.InvestigationDashboardViewModel();
            if (Investigation == null) return;

            // Build summary from InvestigationSummary to avoid duplicating logic
            try
            {
                var summary = EngineeringDiscovery.Core.Domain.Investigation.InvestigationSummary.CreateFrom(Investigation);
                ViewModel.Summary = new EngineeringDiscovery.Web.Components.Dashboard.ViewModels.SummaryCard
                {
                    Repository = summary.RepositoryName,
                    Solution = Investigation.Target,
                    Target = Investigation.Target,
                    ProjectCount = summary.ProjectCount,
                    NamespaceCount = summary.NamespaceCount,
                    TypeCount = summary.TypeCount,
                    MemberCount = summary.MemberCount,
                    TechnologyCount = 0,
                    FindingCount = Investigation.Findings?.Count ?? 0
                };
            }
            catch
            {
                ViewModel.Summary = new EngineeringDiscovery.Web.Components.Dashboard.ViewModels.SummaryCard
                {
                    Repository = Investigation.RepositoryPath,
                    Solution = Investigation.Target,
                    Target = Investigation.Target,
                    ProjectCount = 0,
                    NamespaceCount = 0,
                    TypeCount = 0,
                    MemberCount = 0,
                    TechnologyCount = 0,
                    FindingCount = Investigation.Findings?.Count ?? 0
                };
            }

            // Require structured observations for structural dashboard projection
            var hasTypes = Investigation.TypeObservations != null && Investigation.TypeObservations.Any();
            var hasNamespaces = Investigation.NamespaceObservations != null && Investigation.NamespaceObservations.Any();
            var hasMembers = Investigation.MemberObservations != null && Investigation.MemberObservations.Any();

            if (!hasTypes && !hasNamespaces && !hasMembers)
            {
                throw new InvalidOperationException("Investigation missing structured observations: TypeObservations, NamespaceObservations, or MemberObservations are required for structural dashboard projection.");
            }

            var projects = new Dictionary<string, ProjectNodeViewModel>(StringComparer.OrdinalIgnoreCase);
            var dependencies = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var technologies = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var findings = new List<EngineeringDiscovery.Core.Domain.Investigation.Finding>();

            // Helper: lookup descriptive DiscoveryObservation entries
            var discoveryObs = Investigation.Observations ?? Array.Empty<DiscoveryObservation>();

            IEnumerable<string> GetTypeDescriptions(EngineeringDiscovery.Core.Models.TypeObservation t)
            {
                return discoveryObs.Where(o => o.Kind == ObservationKind.Type
                        && string.Equals(o.Project ?? string.Empty, t.Project ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(o.Namespace ?? string.Empty, t.Namespace ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(o.Type ?? string.Empty, t.TypeName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    .Select(o => o.Description)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
            }

            IEnumerable<string> GetNamespaceDescriptions(EngineeringDiscovery.Core.Models.NamespaceObservation n)
            {
                return discoveryObs.Where(o => o.Kind == ObservationKind.Namespace
                        && string.Equals(o.Project ?? string.Empty, n.Project ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(o.Namespace ?? string.Empty, n.NamespaceName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    .Select(o => o.Description)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
            }

            IEnumerable<string> GetMemberDescriptions(EngineeringDiscovery.Core.Models.MemberObservation m)
            {
                return discoveryObs.Where(o => o.Kind == ObservationKind.Member
                        && string.Equals(o.Project ?? string.Empty, m.Project ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(o.Type ?? string.Empty, m.Type ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(o.Member ?? string.Empty, m.MemberName ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                    .Select(o => o.Description)
                    .Where(d => !string.IsNullOrWhiteSpace(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
            }

            // Build structural tree from canonical observations
            if (hasTypes)
            {
                foreach (var t in Investigation.TypeObservations)
                {
                    var pname = string.IsNullOrWhiteSpace(t.Project) ? "<unknown>" : t.Project;
                    if (!projects.ContainsKey(pname)) projects[pname] = new ProjectNodeViewModel { Name = pname };
                    var proj = projects[pname];

                    var nsName = string.IsNullOrWhiteSpace(t.Namespace) ? "<global>" : t.Namespace;
                    if (!proj.Namespaces.ContainsKey(nsName)) proj.Namespaces[nsName] = new NamespaceNodeViewModel { Name = nsName };
                    var nsModel = proj.Namespaces[nsName];

                    var typeName = t.TypeName ?? "<unknown>";
                    if (!nsModel.Types.ContainsKey(typeName)) nsModel.Types[typeName] = new TypeNodeViewModel { Name = typeName, Kind = t.Kind.ToString().ToLowerInvariant() };
                    var typeModel = nsModel.Types[typeName];

                    var descs = GetTypeDescriptions(t).ToList();
                    if (descs.Any()) typeModel.Observations.AddRange(descs);
                    else typeModel.Observations.Add($"Defines {t.Kind.ToString().ToLowerInvariant()} '{typeName}' in namespace '{nsName}'.");
                }
            }

            if (hasNamespaces)
            {
                foreach (var n in Investigation.NamespaceObservations)
                {
                    var pname = string.IsNullOrWhiteSpace(n.Project) ? "<unknown>" : n.Project;
                    if (!projects.ContainsKey(pname)) projects[pname] = new ProjectNodeViewModel { Name = pname };
                    var proj = projects[pname];

                    var nsName = string.IsNullOrWhiteSpace(n.NamespaceName) ? "<global>" : n.NamespaceName;
                    if (!proj.Namespaces.ContainsKey(nsName)) proj.Namespaces[nsName] = new NamespaceNodeViewModel { Name = nsName };
                    var nsModel = proj.Namespaces[nsName];

                    var descs = GetNamespaceDescriptions(n).ToList();
                    if (descs.Any()) nsModel.Observations.AddRange(descs);
                    else nsModel.Observations.Add($"Namespace '{nsName}'.");
                }
            }

            if (hasMembers)
            {
                foreach (var m in Investigation.MemberObservations)
                {
                    var pname = string.IsNullOrWhiteSpace(m.Project) ? "<unknown>" : m.Project;
                    if (!projects.ContainsKey(pname)) projects[pname] = new ProjectNodeViewModel { Name = pname };
                    var proj = projects[pname];

                    var nsName = string.IsNullOrWhiteSpace(m.Namespace) ? "<global>" : m.Namespace;
                    if (!proj.Namespaces.ContainsKey(nsName)) proj.Namespaces[nsName] = new NamespaceNodeViewModel { Name = nsName };
                    var nsModel = proj.Namespaces[nsName];

                    var typeName = string.IsNullOrWhiteSpace(m.Type) ? "<unknown>" : m.Type;
                    if (!nsModel.Types.ContainsKey(typeName)) nsModel.Types[typeName] = new TypeNodeViewModel { Name = typeName, Kind = "class" };
                    var typeModel = nsModel.Types[typeName];

                    if (!string.IsNullOrWhiteSpace(m.MemberName) && !typeModel.Members.Contains(m.MemberName)) typeModel.Members.Add(m.MemberName);

                    var descs = GetMemberDescriptions(m).ToList();
                    if (descs.Any()) typeModel.Observations.AddRange(descs);
                    else if (!string.IsNullOrWhiteSpace(m.MemberName)) typeModel.Observations.Add($"Defines member '{m.MemberName}' in type '{typeName}'.");
                }
            }

            // Preserve non-structural findings and collect technology/dependency signals via regex
            foreach (var f in Investigation.Findings ?? Enumerable.Empty<Finding>())
            {
                if (f.Type != FindingType.Observation) findings.Add(f);
                var desc = f.Description ?? string.Empty;

                var depRegex = new Regex("references package '(?<pkg>[^']+)'", RegexOptions.IgnoreCase);
                foreach (Match dm in depRegex.Matches(desc)) dependencies.Add(dm.Groups["pkg"].Value.Trim());

                var frameRegex = new Regex("references framework '(?<fw>[^']+)'", RegexOptions.IgnoreCase);
                foreach (Match dm in frameRegex.Matches(desc)) dependencies.Add(dm.Groups["fw"].Value.Trim());

                var analyzerRegex = new Regex("references analyzer '(?<an>[^']+)'", RegexOptions.IgnoreCase);
                foreach (Match dm in analyzerRegex.Matches(desc)) dependencies.Add(dm.Groups["an"].Value.Trim());

                var usesRegex = new Regex("uses (?<tech>[A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase);
                foreach (Match um in usesRegex.Matches(desc))
                {
                    var tech = um.Groups["tech"].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(tech)) technologies.Add(tech);
                }
            }

            ViewModel.Projects = projects;
            ViewModel.Dependencies = dependencies.ToList();
            ViewModel.Technologies = technologies.ToList();
            ViewModel.Findings = findings;
        }

        // Selection model for details panel
        protected enum NodeType { None, Project, Namespace, Type, Member }
        protected NodeType SelectedNodeType { get; set; } = NodeType.None;
        protected string SelectedDetailsTitle { get; set; } = string.Empty;
        protected Dictionary<string, object> SelectedDetails { get; } = new();
        protected List<string>? SelectedObservations { get; set; }

        protected void SelectProject(string project)
        {
            SelectedNodeType = NodeType.Project;
            SelectedDetailsTitle = project;
            SelectedDetails.Clear();
            var p = ViewModel!.Projects[project];
            SelectedDetails["Name"] = p.Name;
            SelectedDetails["Project"] = project;
            SelectedDetails["Type"] = "Project";
            SelectedObservations = p.Observations.ToList();
        }

        protected void SelectNamespace(string project, string ns)
        {
            SelectedNodeType = NodeType.Namespace;
            SelectedDetailsTitle = ns;
            SelectedDetails.Clear();
            var n = ViewModel!.Projects[project].Namespaces[ns];
            SelectedDetails["Name"] = n.Name;
            SelectedDetails["Project"] = project;
            SelectedObservations = n.Observations.ToList();
        }

        protected void SelectType(string project, string ns, string type)
        {
            SelectedNodeType = NodeType.Type;
            SelectedDetailsTitle = type;
            SelectedDetails.Clear();
            var t = ViewModel!.Projects[project].Namespaces[ns].Types[type];
            SelectedDetails["Name"] = t.Name;
            SelectedDetails["Namespace"] = ns;
            SelectedDetails["Kind"] = t.Kind;
            SelectedObservations = t.Observations.ToList();
        }

        protected void SelectMember(string project, string ns, string type, string member)
        {
            SelectedNodeType = NodeType.Member;
            SelectedDetailsTitle = member;
            SelectedDetails.Clear();
            SelectedDetails["Name"] = member;
            SelectedDetails["MemberType"] = "Member";
            SelectedObservations = new List<string>();
        }

        protected bool ProjectMatches(KeyValuePair<string, ProjectNodeViewModel> p) => MatchesFilter(p.Key) || p.Value.Namespaces.Values.Any(n => NamespaceMatches(n));
        protected bool NamespaceMatches(NamespaceNodeViewModel n) => MatchesFilter(n.Name) || n.Types.Values.Any(t => TypeMatches(t));
        protected bool TypeMatches(TypeNodeViewModel t) => MatchesFilter(t.Name) || t.Members.Any(m => MatchesFilter(m));

        protected string? ExtractProjectFromDesc(string desc)
        {
            var m = Regex.Match(desc, "Project '\\\\s*(?<p>[^']+)'", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups["p"].Value.Trim();
            var m2 = Regex.Match(desc, "Project (?<p>[A-Za-z0-9_.-]+)", RegexOptions.IgnoreCase);
            if (m2.Success) return m2.Groups["p"].Value.Trim();
            return null;
        }

        protected string KindFromDesc(string desc)
        {
            if (desc.IndexOf("interface", StringComparison.OrdinalIgnoreCase) >= 0) return "interface";
            if (desc.IndexOf("record", StringComparison.OrdinalIgnoreCase) >= 0) return "record";
            if (desc.IndexOf("struct", StringComparison.OrdinalIgnoreCase) >= 0) return "struct";
            if (desc.IndexOf("enum", StringComparison.OrdinalIgnoreCase) >= 0) return "enum";
            if (desc.IndexOf("delegate", StringComparison.OrdinalIgnoreCase) >= 0) return "delegate";
            return "class";
        }

        protected bool MatchesFilter(string text)
        {
            if (string.IsNullOrWhiteSpace(SearchTerm)) return true;
            return text.IndexOf(SearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Provide namespace id for expand/collapse tracking
        public string GetNamespaceIdPublic(string project, string ns) => project + ":" + ns;
        public string GetTypeIdPublic(string project, string ns, string type) => project + ":" + ns + ":" + type;

        protected void ToggleProject(string project)
        {
            if (!ExpandedProjects.Remove(project)) ExpandedProjects.Add(project);
        }

        protected void ToggleNamespace(string project, string ns)
        {
            var id = GetNamespaceIdPublic(project, ns);
            if (!ExpandedNamespaces.Remove(id)) ExpandedNamespaces.Add(id);
        }

        protected void ToggleType(string project, string ns, string type)
        {
            var id = GetTypeIdPublic(project, ns, type);
            if (!ExpandedTypes.Remove(id)) ExpandedTypes.Add(id);
        }

        // ViewModel types have been moved to EngineeringDiscovery.Web.Components.Dashboard.ViewModels
    }
}
