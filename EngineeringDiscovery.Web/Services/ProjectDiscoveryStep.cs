using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace EngineeringDiscovery.Web.Services
{
    internal class ProjectDiscoveryStep : IInvestigationStep
    {
        public InvestigationPhase Phase => InvestigationPhase.Discovery;
        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            var solutionPath = context.SolutionPath;
            if (string.IsNullOrWhiteSpace(solutionPath) || !File.Exists(solutionPath)) return;

            try
            {
                var lines = File.ReadAllLines(solutionPath);
                var solutionDir = Path.GetDirectoryName(solutionPath) ?? string.Empty;
                var solutionExtension = Path.GetExtension(solutionPath);

                if (string.Equals(solutionExtension, ".slnx", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var slnxDoc = XDocument.Load(solutionPath);
                        var projectElements = slnxDoc
                            .Descendants()
                            .Where(x => string.Equals(x.Name.LocalName, "Project", StringComparison.OrdinalIgnoreCase));

                        foreach (var pe in projectElements)
                        {
                            try
                            {
                                var pathAttr = pe.Attribute("Path")?.Value;
                                if (string.IsNullOrWhiteSpace(pathAttr)) continue;
                                if (pathAttr.IndexOf(".csproj", StringComparison.OrdinalIgnoreCase) < 0) continue;

                                var projectPath = pathAttr;
                                if (!Path.IsPathRooted(projectPath))
                                {
                                    projectPath = Path.GetFullPath(Path.Combine(solutionDir, projectPath));
                                }

                                var nameAttr = pe.Attribute("Name")?.Value;
                                var projectName = !string.IsNullOrWhiteSpace(nameAttr)
                                    ? nameAttr
                                    : (Path.GetFileNameWithoutExtension(projectPath) ?? "Unnamed");

                                context.DiscoveredProjects.Add((Name: projectName, Path: projectPath));
                            }
                            catch (Exception ex)
                            {
                                context.AddDiagnostic($"Failed to parse project entry in .slnx: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        context.AddDiagnostic($"Failed parsing .slnx for projects: {ex.Message}");
                    }
                }
                else
                {
                    var projectLines = lines.Where(l => l.IndexOf(".csproj", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    foreach (var pl in projectLines)
                    {
                        try
                        {
                            var parts = pl.Split('=');
                            if (parts.Length < 2) continue;
                            var rhs = parts[1];
                            var segments = rhs.Split(',').Select(p => p.Trim()).ToArray();
                            if (segments.Length < 2) continue;
                            var namePart = segments[0].Trim();
                            var pathPart = segments[1].Trim();
                            if (namePart.StartsWith("\"")) namePart = namePart.Trim('"');
                            if (pathPart.StartsWith("\"")) pathPart = pathPart.Trim('"');

                            var projectPath = pathPart;
                            if (!Path.IsPathRooted(projectPath))
                            {
                                projectPath = Path.GetFullPath(Path.Combine(solutionDir, projectPath));
                            }

                            context.DiscoveredProjects.Add((Name: namePart, Path: projectPath));
                        }
                        catch
                        {
                            context.AddDiagnostic($"Failed to parse project line: {pl}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                context.AddDiagnostic($"Failed to read solution file during project enumeration: {ex.Message}");
            }
        }
    }
}
