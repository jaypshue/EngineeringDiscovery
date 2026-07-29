using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services
{
    public class SolutionDiscoveryEngine : IDiscoveryEngine
    {
        private readonly string? solutionPath;

        public SolutionDiscoveryEngine(string? solutionPath = null)
        {
            this.solutionPath = solutionPath ?? FindSolutionInParents();
        }

        public Investigation CreateInvestigation(string? targetOverride = null)
        {
            // Default sample values
            var defaultGoal = "Assess repository for maintainability and security risks.";
            var defaultOwner = "alice@example.com";
            var defaultTarget = "engineering-discovery";

            string target = defaultTarget;
            var discoveredProjects = new List<(string Name, string Path)>();

            if (!string.IsNullOrEmpty(solutionPath) && File.Exists(solutionPath))
            {
                try
                {
                    var fileName = Path.GetFileNameWithoutExtension(solutionPath) ?? defaultTarget;
                    target = fileName;

                    var lines = File.ReadAllLines(solutionPath);

                    // Look for lines that reference project files and try to extract the project name and path
                    var projectLines = lines.Where(l => l.IndexOf(".csproj", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                    var solutionDir = Path.GetDirectoryName(solutionPath) ?? Directory.GetCurrentDirectory();
                    foreach (var pl in projectLines)
                    {
                        try
                        {
                            // Typical .sln project line: Project("{...}") = "Name", "path\to\project.csproj", "{GUID}"
                            var parts = pl.Split('=');
                            if (parts.Length < 2) continue;
                            var rhs = parts[1];
                            var segments = rhs.Split(',').Select(p => p.Trim()).ToArray();
                            if (segments.Length < 2) continue;
                            var namePart = segments[0].Trim();
                            var pathPart = segments[1].Trim();
                            if (namePart.StartsWith("\"")) namePart = namePart.Trim('"');
                            if (pathPart.StartsWith("\"")) pathPart = pathPart.Trim('"');

                            // Resolve relative project path against the solution directory
                            var projectPath = pathPart;
                            if (!Path.IsPathRooted(projectPath))
                            {
                                projectPath = Path.GetFullPath(Path.Combine(solutionDir, projectPath));
                            }

                            discoveredProjects.Add((Name: namePart, Path: projectPath));
                        }
                        catch
                        {
                            // ignore individual parse errors
                        }
                    }
                }
                catch
                {
                    // Fall back to defaults on any IO error
                    target = defaultTarget;
                    discoveredProjects.Clear();
                }
            }

            // allow the caller (UI) to override the discovered target
            if (!string.IsNullOrWhiteSpace(targetOverride)) target = targetOverride;

            // Solution directory (if available) for solution-level discovery
            var solutionDirLocal = !string.IsNullOrWhiteSpace(solutionPath) ? Path.GetDirectoryName(solutionPath) : null;

            var inv = Investigation.Create(
                Guid.NewGuid(),
                repositoryPath: "/",
                goal: defaultGoal,
                owner: defaultOwner,
                target: target,
                architectureStatus: EngineeringStageStatus.NotStarted,
                planningStatus: EngineeringStageStatus.NotStarted,
                developmentStatus: EngineeringStageStatus.NotStarted,
                verificationStatus: EngineeringStageStatus.NotStarted);

            // Preserve previous behavior: start investigation and seed sample findings
            inv.Start();
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Architecture, "API follows layered architecture."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Risk, "Authentication library is deprecated."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Decision, "WorkspaceHost owns the Investigation aggregate."));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Question, "Which authentication provider should we adopt?"));
            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.TechnicalDebt, "Legacy authentication module requires refactoring."));

            // Add observation about solution/project count when discovered
            var projectCount = discoveredProjects.Count;
            if (projectCount > 0)
            {
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {projectCount} projects."));
            }

            // Infer project types using simple filename/name conventions and add observations
            foreach (var proj in discoveredProjects)
            {
                try
                {
                    var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                    var lowered = name.ToLowerInvariant();
                    string projType;

                    if (lowered.Contains("test") || lowered.Contains("tests")) projType = "Test Project";
                    else if (lowered.Contains("web") || lowered.Contains("api")) projType = "Web";
                    else if (lowered.Contains("console") || lowered.Contains("app")) projType = "Console";
                    else if (lowered.Contains("core") || lowered.Contains("lib") || lowered.Contains("common") || lowered.Contains("shared")) projType = "Class Library";
                    else projType = "Unknown";

                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} (Project Type: {projType})"));
                }
                catch
                {
                    // ignore per-project errors
                }
            }

            // Project reference discovery: inspect each discovered .csproj for ProjectReference elements
            foreach (var proj in discoveredProjects)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(proj.Path)) continue;
                    var projFile = proj.Path;
                    if (!File.Exists(projFile)) continue;

                    var doc = XDocument.Load(projFile);
                    // Find ProjectReference elements in the XML namespace-agnostic way
                    var projectReferences = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase));
                    foreach (var pr in projectReferences)
                    {
                        try
                        {
                            var includeAttr = pr.Attribute("Include")?.Value;
                            if (string.IsNullOrWhiteSpace(includeAttr)) continue;

                            // Resolve referenced project path relative to the source project's directory
                            var sourceDir = Path.GetDirectoryName(projFile) ?? Directory.GetCurrentDirectory();
                            var referencedPath = includeAttr;
                            if (!Path.IsPathRooted(referencedPath)) referencedPath = Path.GetFullPath(Path.Combine(sourceDir, referencedPath));
                            else referencedPath = Path.GetFullPath(referencedPath);

                            // Try to find a discovered project that matches the referenced path
                            var referencedProject = discoveredProjects.FirstOrDefault(d =>
                                !string.IsNullOrEmpty(d.Path) &&
                                string.Equals(Path.GetFullPath(d.Path), referencedPath, StringComparison.OrdinalIgnoreCase));

                            var sourceName = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                            var referencedName = referencedProject.Name ?? Path.GetFileNameWithoutExtension(referencedPath) ?? "Unnamed";

                            // Only add a finding if the referenced project is different from the source
                            if (!string.IsNullOrEmpty(referencedName) && !string.Equals(sourceName, referencedName, StringComparison.OrdinalIgnoreCase))
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{sourceName} references {referencedName}."));
                            }
                        }
                        catch
                        {
                            // ignore individual project reference parse errors
                                                        
                            //Eventually:
                            //    FindingType.Warning
                            //    Unable to inspect project:
                            //    EngineeringDiscovery.Web.csproj

                        }
                    }
                }
                catch
                {
                    // ignore per-project inspection errors
                }
            }

            // Technology discovery: inspect each project for SDK, TargetFramework(s), and PackageReferences
            var packageIndicators = new[]
            {
                "entityframeworkcore",
                "efcore",
                "serilog",
                "automapper",
                "mediatR".ToLowerInvariant(),
                "xunit",
                "nunit",
                "mstest",
                "fluentvalidation",
                "swashbuckle",
                "signalr",
            };

            foreach (var proj in discoveredProjects)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(proj.Path)) continue;
                    var projFile = proj.Path;
                    if (!File.Exists(projFile)) continue;

                    var doc = XDocument.Load(projFile);

                    var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";

                    // Project SDK (from Project/@Sdk)
                    try
                    {
                        var sdkAttr = doc.Root?.Attribute("Sdk")?.Value;
                        if (!string.IsNullOrWhiteSpace(sdkAttr))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} uses {sdkAttr}."));
                        }
                    }
                    catch { }

                    // Target frameworks
                    try
                    {
                        var tfElems = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "TargetFramework", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(x.Name.LocalName, "TargetFrameworks", StringComparison.OrdinalIgnoreCase));
                        foreach (var tf in tfElems)
                        {
                            var tfValue = (tf?.Value ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(tfValue)) continue;

                            // TargetFrameworks can be semicolon-separated
                            var frameworks = tfValue.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
                            foreach (var f in frameworks)
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project {name} targets {f}."));
                            }
                        }
                    }
                    catch { }

                    // PackageReferences (detect common packages)
                    try
                    {
                        var packageRefs = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase));
                        foreach (var pr in packageRefs)
                        {
                            var include = pr.Attribute("Include")?.Value ?? pr.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Include", StringComparison.OrdinalIgnoreCase))?.Value;
                            if (string.IsNullOrWhiteSpace(include)) continue;

                            var lowered = include.ToLowerInvariant();
                            // If package matches a known indicator, add a finding
                            foreach (var indicator in packageIndicators)
                            {
                                if (lowered.Contains(indicator))
                                {
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} references {include}."));
                                    break;
                                }
                            }
                        }
                    }
                    catch { }

                    // Solution Structure Discovery: project folder, assembly name, root namespace, output type, project type, potential startup
                    try
                    {
                        var projectFolder = Path.GetDirectoryName(proj.Path) ?? string.Empty;

                        // AssemblyName
                        var assemblyName = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "AssemblyName", StringComparison.OrdinalIgnoreCase))?.Value;

                        // RootNamespace
                        var rootNamespace = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "RootNamespace", StringComparison.OrdinalIgnoreCase))?.Value;

                        // OutputType
                        var outputType = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "OutputType", StringComparison.OrdinalIgnoreCase))?.Value;

                        if (!string.IsNullOrWhiteSpace(projectFolder))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' is located in folder '{projectFolder}'."));
                        }

                        if (!string.IsNullOrWhiteSpace(assemblyName))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' has assembly name '{assemblyName}'."));
                        }

                        if (!string.IsNullOrWhiteSpace(rootNamespace))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' has root namespace '{rootNamespace}'."));
                        }

                        if (!string.IsNullOrWhiteSpace(outputType))
                        {
                            var ot = outputType.Trim();
                            if (string.Equals(ot, "Exe", StringComparison.OrdinalIgnoreCase) || string.Equals(ot, "exe", StringComparison.OrdinalIgnoreCase))
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' is an executable application."));
                            }
                            else if (string.Equals(ot, "Library", StringComparison.OrdinalIgnoreCase) || string.Equals(ot, "library", StringComparison.OrdinalIgnoreCase))
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' is a class library."));
                            }
                            else
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' has output type '{ot}'."));
                            }
                        }

                        // Determine project type (more specific classification)
                        string detailedType;
                        var loweredName = name.ToLowerInvariant();
                        var sdkAttrSmall = doc.Root?.Attribute("Sdk")?.Value ?? string.Empty;

                        if (!string.IsNullOrWhiteSpace(sdkAttrSmall) && sdkAttrSmall.IndexOf("microsoft.net.sdk.web", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            detailedType = "Web Application";
                        }
                        else if (!string.IsNullOrWhiteSpace(outputType) && outputType.IndexOf("exe", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            detailedType = "Console Application";
                        }
                        else if (loweredName.Contains("test") || loweredName.Contains("tests"))
                        {
                            detailedType = "Test Project";
                        }
                        else if (loweredName.Contains("web") || loweredName.Contains("api"))
                        {
                            detailedType = "Web Application";
                        }
                        else
                        {
                            detailedType = "Class Library";
                        }

                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' appears to be a {detailedType}."));

                        // Potential startup project
                        var isPotentialStartup = (detailedType == "Web Application") || (!string.IsNullOrWhiteSpace(outputType) && outputType.IndexOf("exe", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (isPotentialStartup)
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' is a potential startup application."));
                        }
                        // Startup & Entry Point Discovery: inspect common entry files and bootstrap patterns
                        try
                        {
                            projectFolder = Path.GetDirectoryName(proj.Path) ?? string.Empty;
                            var programPath = Path.Combine(projectFolder, "Program.cs");
                            var startupPath = Path.Combine(projectFolder, "Startup.cs");

                            if (File.Exists(programPath))
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' contains Program.cs."));
                                string programText = File.ReadAllText(programPath);
                                var textLower = programText.ToLowerInvariant();

                                var usesWebAppCreate = programText.IndexOf("WebApplication.CreateBuilder", StringComparison.OrdinalIgnoreCase) >= 0;
                                if (usesWebAppCreate)
                                {
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' creates a WebApplication using WebApplication.CreateBuilder()."));
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' uses the ASP.NET Core minimal hosting model."));
                                }

                                var usesWebAppBuild = programText.IndexOf("WebApplication.Build", StringComparison.OrdinalIgnoreCase) >= 0;
                                if (usesWebAppBuild)
                                {
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' calls WebApplication.Build()."));
                                }

                                var usesHostCreate = programText.IndexOf("Host.CreateDefaultBuilder", StringComparison.OrdinalIgnoreCase) >= 0;
                                if (usesHostCreate)
                                {
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' uses Host.CreateDefaultBuilder()."));
                                }

                                var usesHostAppBuilder = programText.IndexOf("HostApplicationBuilder", StringComparison.OrdinalIgnoreCase) >= 0;
                                if (usesHostAppBuilder)
                                {
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' uses HostApplicationBuilder."));
                                }

                                var hasStaticMain = programText.IndexOf("static void Main(", StringComparison.OrdinalIgnoreCase) >= 0;
                                if (hasStaticMain)
                                {
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' contains a static Main entry point."));
                                }

                                // Top-level statements heuristic: Program.cs exists but no static Main and contains builder or statements
                                if (!hasStaticMain && (usesWebAppCreate || programText.IndexOf("var builder", StringComparison.OrdinalIgnoreCase) >= 0 || programText.IndexOf("var app", StringComparison.OrdinalIgnoreCase) >= 0))
                                {
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' appears to use top-level statements for its entry point."));
                                }
                            }

                            if (File.Exists(startupPath))
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' contains Startup.cs."));
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' uses a Startup class for application bootstrap."));
                            }

                            // Configuration discovery per-project: appsettings, launchSettings, UserSecretsId, environment usage, config providers
                            try
                            {
                                // appsettings files
                                if (!string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder))
                                {
                                    var appSettings = Directory.GetFiles(projectFolder, "appsettings*.json", SearchOption.TopDirectoryOnly);
                                    foreach (var af in appSettings)
                                    {
                                        var fileName = Path.GetFileName(af);
                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' contains {fileName}."));
                                    }

                                    // launchSettings.json under Properties
                                    var launch = Path.Combine(projectFolder, "Properties", "launchSettings.json");
                                    if (File.Exists(launch))
                                    {
                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' contains launchSettings.json."));
                                    }
                                }

                                // UserSecretsId in csproj
                                try
                                {
                                    var userSecretsId = doc.Descendants().FirstOrDefault(x => string.Equals(x.Name.LocalName, "UserSecretsId", StringComparison.OrdinalIgnoreCase))?.Value;
                                    if (!string.IsNullOrWhiteSpace(userSecretsId))
                                    {
                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' defines a User Secrets identifier."));
                                    }
                                }
                                catch { }

                                // Environment usage and configuration providers (from Program.cs if present)
                                try
                                {
                                    if (File.Exists(programPath))
                                    {
                                        var programText2 = File.ReadAllText(programPath);
                                        if (programText2.IndexOf("ASPNETCORE_ENVIRONMENT", StringComparison.OrdinalIgnoreCase) >= 0
                                            || programText2.IndexOf("GetEnvironmentVariable", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references environment variables or ASP.NET Core environment usage."));
                                        }

                                        if (programText2.IndexOf("AddJsonFile", StringComparison.OrdinalIgnoreCase) >= 0
                                            || programText2.IndexOf("AddEnvironmentVariables", StringComparison.OrdinalIgnoreCase) >= 0
                                            || programText2.IndexOf("ConfigureAppConfiguration", StringComparison.OrdinalIgnoreCase) >= 0)
                                        {
                                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' configures configuration providers in Program.cs."));
                                        }
                                    }
                                }
                                catch { }
                            }
                            catch { }
                        }
                        catch { }
                    }
                    catch { }
                }
                catch
                {
                    // ignore per-project tech discovery errors
                }
            }

            // Architectural layer inference: infer a layer for each discovered project and report relationships
            try
            {
                // Helper to infer a layer string from project name and project file SDK
                string InferLayer(string projName, string projFile)
                {
                    var lowered = (projName ?? string.Empty).ToLowerInvariant();
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(projFile) && File.Exists(projFile))
                        {
                            var doc = XDocument.Load(projFile);
                            var sdkAttr = doc.Root?.Attribute("Sdk")?.Value ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(sdkAttr) && sdkAttr.IndexOf("microsoft.net.sdk.web", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return "Web layer";
                            }
                        }
                    }
                    catch { }

                    if (lowered.EndsWith(".api") || lowered.Contains(".api")) return "API layer";
                    if (lowered.Contains("web")) return "Web layer";
                    if (lowered.Contains("core")) return "Core domain layer";
                    if (lowered.Contains("infrastructure")) return "Infrastructure layer";
                    if (lowered.Contains("tests") || lowered.Contains("test")) return "Test layer";
                    if (lowered.Contains("shared")) return "Shared layer";
                    return "Unknown layer";
                }

                // Map project name -> layer
                var projectLayer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var proj in discoveredProjects)
                {
                    try
                    {
                        var name = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                        var layer = InferLayer(name, proj.Path);
                        projectLayer[name] = layer;
                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"{name} appears to be the {layer}."));
                    }
                    catch { }
                }

                // Now inspect references again to produce layer-to-layer relationship findings
                var relationshipSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var proj in discoveredProjects)
                {
                    try
                    {
                        if (string.IsNullOrWhiteSpace(proj.Path)) continue;
                        var projFile = proj.Path;
                        if (!File.Exists(projFile)) continue;

                        var doc = XDocument.Load(projFile);
                        var projectReferences = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase));
                        var sourceName = proj.Name ?? Path.GetFileNameWithoutExtension(proj.Path) ?? "Unnamed";
                        var sourceLayer = projectLayer.ContainsKey(sourceName) ? projectLayer[sourceName] : "Unknown layer";

                        foreach (var pr in projectReferences)
                        {
                            try
                            {
                                var includeAttr = pr.Attribute("Include")?.Value;
                                if (string.IsNullOrWhiteSpace(includeAttr)) continue;
                                var sourceDir = Path.GetDirectoryName(projFile) ?? Directory.GetCurrentDirectory();
                                var referencedPath = includeAttr;
                                if (!Path.IsPathRooted(referencedPath)) referencedPath = Path.GetFullPath(Path.Combine(sourceDir, referencedPath));
                                else referencedPath = Path.GetFullPath(referencedPath);

                                var referencedProject = discoveredProjects.FirstOrDefault(d =>
                                    !string.IsNullOrEmpty(d.Path) &&
                                    string.Equals(Path.GetFullPath(d.Path), referencedPath, StringComparison.OrdinalIgnoreCase));

                                var referencedName = referencedProject.Name ?? Path.GetFileNameWithoutExtension(referencedPath) ?? "Unnamed";
                                var referencedLayer = projectLayer.ContainsKey(referencedName) ? projectLayer[referencedName] : "Unknown layer";

                                if (!string.Equals(sourceLayer, referencedLayer, StringComparison.OrdinalIgnoreCase))
                                {
                                    var rel = $"{sourceLayer} depends on {referencedLayer}.";
                                    if (!relationshipSet.Contains(rel))
                                    {
                                        relationshipSet.Add(rel);
                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, rel));
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return inv;
        }

        private static string? FindSolutionInParents()
        {
            try
            {
                var dir = AppContext.BaseDirectory ?? Directory.GetCurrentDirectory();
                var info = new DirectoryInfo(dir);
                while (info != null)
                {
                    var sln = info.GetFiles("*.sln*").FirstOrDefault();
                    if (sln != null) return sln.FullName;
                    info = info.Parent;
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }
    }
}
