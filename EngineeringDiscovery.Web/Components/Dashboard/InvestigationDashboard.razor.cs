using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Web.Components.Dashboard.ViewModels;

namespace EngineeringDiscovery.Web.Components.Dashboard
{
    public partial class InvestigationDashboard : ComponentBase
    {
        [Parameter]
        public Investigation? Investigation { get; set; }

        protected EngineeringDiscovery.Web.Components.Dashboard.ViewModels.InvestigationDashboardViewModel? ViewModel { get; set; }

        protected string SearchTerm { get; set; } = string.Empty;

        protected HashSet<string> ExpandedProjects { get; } = new();
        protected HashSet<string> ExpandedNamespaces { get; } = new();
        protected HashSet<string> ExpandedTypes { get; } = new();

        protected override void OnParametersSet()
        {
            base.OnParametersSet();
            BuildViewModel();
        }

        protected void BuildViewModel()
        {
            ViewModel = new EngineeringDiscovery.Web.Components.Dashboard.ViewModels.InvestigationDashboardViewModel();
            if (Investigation == null) return;

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

            var projects = new Dictionary<string, ProjectNodeViewModel>(StringComparer.OrdinalIgnoreCase);
            var dependencies = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var technologies = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            var findings = new List<EngineeringDiscovery.Core.Domain.Investigation.Finding>();

            foreach (var f in Investigation.Findings ?? Enumerable.Empty<Finding>())
            {
                if (f.Type != FindingType.Observation) findings.Add(f);
                var desc = f.Description ?? string.Empty;

                foreach (Match m in Regex.Matches(desc, "Project '\\\\s*(?<p>[^']+)'", RegexOptions.IgnoreCase))
                {
                    var pname = m.Groups["p"].Value.Trim();
                    if (!projects.ContainsKey(pname)) projects[pname] = new ProjectNodeViewModel { Name = pname };
                    projects[pname].Observations.Add(desc);
                }

                foreach (Match m in Regex.Matches(desc, "Project (?<p>[A-Za-z0-9_.-]+) ", RegexOptions.IgnoreCase))
                {
                    var pname = m.Groups["p"].Value.Trim();
                    if (!projects.ContainsKey(pname)) projects[pname] = new ProjectNodeViewModel { Name = pname };
                    projects[pname].Observations.Add(desc);
                }

                var nsMatches = Regex.Matches(desc, "defines namespace '\\\\s*(?<ns>[^']+)'", RegexOptions.IgnoreCase);
                foreach (Match nm in nsMatches)
                {
                    var ns = nm.Groups["ns"].Value.Trim();
                    var p = ExtractProjectFromDesc(desc) ?? "<unknown>";
                    if (!projects.ContainsKey(p)) projects[p] = new ProjectNodeViewModel { Name = p };
                    var proj = projects[p];
                    if (!proj.Namespaces.ContainsKey(ns)) proj.Namespaces[ns] = new NamespaceNodeViewModel { Name = ns };
                    proj.Namespaces[ns].Observations.Add(desc);
                }

                var typeRegex = new Regex("defines (?:class|interface|record|struct|enum|delegate) '\\\\s*(?<type>[^']+)' in namespace '\\\\s*(?<ns>[^']+)'", RegexOptions.IgnoreCase);
                foreach (Match tm in typeRegex.Matches(desc))
                {
                    var typeName = tm.Groups["type"].Value.Trim();
                    var ns = tm.Groups["ns"].Value.Trim();
                    var p = ExtractProjectFromDesc(desc) ?? "<unknown>";
                    if (!projects.ContainsKey(p)) projects[p] = new ProjectNodeViewModel { Name = p };
                    var proj = projects[p];
                    if (!proj.Namespaces.ContainsKey(ns)) proj.Namespaces[ns] = new NamespaceNodeViewModel { Name = ns };
                    var nsModel = proj.Namespaces[ns];
                    if (!nsModel.Types.ContainsKey(typeName)) nsModel.Types[typeName] = new TypeNodeViewModel { Name = typeName, Kind = KindFromDesc(desc) };
                    nsModel.Types[typeName].Observations.Add(desc);
                }

                var memberRegex = new Regex("defines (?<kind>constructor|method|property|field|event) '\\\\s*(?<member>[^']+)' in type '\\\\s*(?<type>[^']+)'", RegexOptions.IgnoreCase);
                foreach (Match mm in memberRegex.Matches(desc))
                {
                    var kind = mm.Groups["kind"].Value.Trim();
                    var member = mm.Groups["member"].Value.Trim();
                    var typeName = mm.Groups["type"].Value.Trim();
                    var p = ExtractProjectFromDesc(desc) ?? "<unknown>";
                    if (!projects.ContainsKey(p)) projects[p] = new ProjectNodeViewModel { Name = p };
                    var proj = projects[p];
                    var tmodel = proj.Namespaces.Values.SelectMany(n => n.Types.Values).FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));
                    if (tmodel == null)
                    {
                        if (!proj.Namespaces.ContainsKey("<global>")) proj.Namespaces["<global>"] = new NamespaceNodeViewModel { Name = "<global>" };
                        var nsModel = proj.Namespaces["<global>"];
                        if (!nsModel.Types.ContainsKey(typeName)) nsModel.Types[typeName] = new TypeNodeViewModel { Name = typeName, Kind = "type" };
                        nsModel.Types[typeName].Members.Add($"{kind}: {member}");
                        nsModel.Types[typeName].Observations.Add(desc);
                    }
                    else
                    {
                        tmodel.Members.Add($"{kind}: {member}");
                        tmodel.Observations.Add(desc);
                    }
                }

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
                    if (!string.IsNullOrWhiteSpace(tech) && tech.Length < 100) technologies.Add(tech);
                }
            }

            ViewModel.Projects = projects;
            ViewModel.Dependencies = dependencies.ToList();
            ViewModel.Technologies = technologies.ToList();
            ViewModel.Findings = findings;

            ViewModel.Summary.ProjectCount = projects.Count;
            ViewModel.Summary.NamespaceCount = projects.Values.SelectMany(p => p.Namespaces.Keys).Distinct().Count();
            ViewModel.Summary.TypeCount = projects.Values.SelectMany(p => p.Namespaces.Values).SelectMany(n => n.Types.Keys).Distinct().Count();
            ViewModel.Summary.MemberCount = projects.Values.SelectMany(p => p.Namespaces.Values).SelectMany(n => n.Types.Values).SelectMany(t => t.Members).Count();
            ViewModel.Summary.TechnologyCount = technologies.Count;
        }

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
