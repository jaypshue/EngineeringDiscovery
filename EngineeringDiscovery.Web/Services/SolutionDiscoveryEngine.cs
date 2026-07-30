using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using EngineeringDiscovery.Core.Domain;
using EngineeringDiscovery.Core.Models;
using System.Text.RegularExpressions;
using EngineeringDiscovery.Core.Domain.Investigation;
using System.Diagnostics;

namespace EngineeringDiscovery.Web.Services
{
    public class SolutionDiscoveryEngine : IDiscoveryEngine
    {
        public SolutionDiscoveryEngine()
        {
        }

        public Investigation CreateInvestigation(string? repositoryRoot = null, string? targetOverride = null)
        {
            // Default sample values
            var defaultGoal = "Assess repository for maintainability and security risks.";
            var defaultOwner = "alice@example.com";
            var defaultTarget = "engineering-discovery";

            string target = defaultTarget;
            var discoveredProjects = new List<(string Name, string Path)>();

            // Create discovery context to share state and diagnostics
            var context = new DiscoveryContext(null);
            // repositoryRoot may be provided by the UI; the engine will search for solutions under it when needed
            var effectiveRepositoryRoot = repositoryRoot;

            // Determine effective solution path based on repositoryRoot
            string? effectiveSolutionPath = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(effectiveRepositoryRoot))
                {
                    // If the provided repositoryRoot is actually a solution file, use it directly
                    if (File.Exists(effectiveRepositoryRoot) && (effectiveRepositoryRoot.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) || effectiveRepositoryRoot.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
                    {
                        effectiveSolutionPath = effectiveRepositoryRoot;
                    }
                    else if (Directory.Exists(effectiveRepositoryRoot))
                    {
                        var sols = FindSolutionsInRepository(effectiveRepositoryRoot);
                        if (sols.Length == 1)
                        {
                            effectiveSolutionPath = sols[0];
                        }
                        else if (sols.Length == 0)
                        {
                            context.AddDiagnostic($"No solution files (*.sln, *.slnx) found under repository root: {effectiveRepositoryRoot}");
                        }
                        else
                        {
                            // Multiple solutions found - choose the first but note the ambiguity
                            context.AddDiagnostic($"Multiple solutions found under repository root: {effectiveRepositoryRoot}. Using first: {Path.GetFileName(sols[0])}");
                            effectiveSolutionPath = sols[0];
                        }
                    }
                }
                else
                {
                    // No repositoryRoot provided - intentionally do not fall back to execution directory.
                    // Discovery requires an explicit repositoryRoot from the UI. Record a diagnostic.
                    context.AddDiagnostic("No repository root provided. Discovery requires an explicit repository root from the UI.");
                }
            }
            catch (Exception ex)
            {
                context.AddDiagnostic($"Failed locating solution: {ex.Message}");
            }

            // Solution-wide type totals
            var totalClasses = 0;
            var totalInterfaces = 0;
            var totalRecords = 0;
            var totalStructs = 0;
            var totalEnums = 0;
            var totalDelegates = 0;
            var totalConstructors = 0;
            var totalMethods = 0;
            var totalProperties = 0;
            var totalFields = 0;
            var totalEvents = 0;

            // Delegate project discovery to the pipeline step
            if (!string.IsNullOrEmpty(effectiveSolutionPath) && File.Exists(effectiveSolutionPath))
            {
                try
                {
                    // set solution path on context so steps can access it
                    context = new DiscoveryContext(effectiveSolutionPath);

                    // run project discovery
                    var projectStep = new ProjectDiscoveryStep();
                    projectStep.Execute(context);

                    // namespace discovery will be invoked after Investigation is created

                    // copy discovered projects into local list for backward compatibility
                    discoveredProjects.AddRange(context.DiscoveredProjects);

                    // determine target from solution filename as before
                    var fileName = Path.GetFileNameWithoutExtension(effectiveSolutionPath) ?? defaultTarget;
                    target = fileName;
                }
                catch (Exception ex)
                {
                    // Fall back to defaults on any IO error
                    target = defaultTarget;
                    discoveredProjects.Clear();
                    context.AddDiagnostic($"Failed during project discovery: {ex.Message}");
                }
            }

            // allow the caller (UI) to override the discovered target
            if (!string.IsNullOrWhiteSpace(targetOverride)) target = targetOverride;

            // Solution directory (if available) for solution-level discovery
            var solutionDirLocal = !string.IsNullOrWhiteSpace(effectiveSolutionPath) ? Path.GetDirectoryName(effectiveSolutionPath) : null;

            var inv = Investigation.Create(
                Guid.NewGuid(),
                repositoryPath: effectiveRepositoryRoot ?? Path.GetDirectoryName(effectiveSolutionPath) ?? "/",
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
            var projectCount = context.DiscoveredProjects.Count;
            if (projectCount > 0)
            {
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {projectCount} projects."));
                inv.AddObservation(new DiscoveryObservation
                {
                    Kind = ObservationKind.Solution,
                    Project = string.Empty,
                    Description = $"Solution contains {projectCount} projects."
                });
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
                    inv.AddObservation(new DiscoveryObservation
                    {
                        Kind = ObservationKind.Project,
                        Project = name,
                        Description = $"{name} (Project Type: {projType})"
                    });
                }
                catch
                {
                    // ignore per-project errors
                }
            }

            // Invoke namespace discovery now that Investigation (inv) exists
            var nsStep = new NamespaceDiscoveryStep(inv);
            nsStep.Execute(context);

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
                            var sourceDir = Path.GetDirectoryName(projFile) ?? throw new InvalidOperationException("Project file path must be rooted or within a discovered solution directory.");
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
                                inv.AddObservation(new DiscoveryObservation
                                {
                                    Kind = ObservationKind.Dependency,
                                    Project = sourceName,
                                    Description = $"{sourceName} references {referencedName}.",
                                    // referencedName is available in description; Namespace/Type/Member not applicable
                                });
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


                    // (Capability detection deferred until packages are collected below)

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

                    // PackageReferences: report every referenced package and collect for capability inference and analyzer detection
                    List<string> discoveredPackages = new List<string>();
                    try
                    {
                        var packageRefs = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase));
                        foreach (var pr in packageRefs)
                        {
                            var include = pr.Attribute("Include")?.Value ?? pr.Elements().FirstOrDefault(e => string.Equals(e.Name.LocalName, "Include", StringComparison.OrdinalIgnoreCase))?.Value;
                            if (string.IsNullOrWhiteSpace(include)) continue;

                            discoveredPackages.Add(include);
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references package '{include}'."));
                        }
                    }
                    catch { }

                    // Capability detection from discovered packages
                    try
                    {
                        var lpacks = discoveredPackages.Select(p => p.ToLowerInvariant()).ToList();
                        if (lpacks.Any(p => p.Contains("signalr")))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references SignalR packages."));
                        }

                        if (lpacks.Any(p => p.Contains("entityframeworkcore") || p.Contains("efcore") || p.Contains("microsoft.entityframeworkcore")))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references Entity Framework Core packages."));
                        }

                        if (lpacks.Any(p => p.Contains("serilog")))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references Serilog packages."));
                        }

                        if (lpacks.Any(p => p.Contains("opentelemetry")))
                        {
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references OpenTelemetry packages."));
                        }
                    }
                    catch { }

                    // FrameworkReference elements
                    try
                    {
                        var frameworkRefs = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "FrameworkReference", StringComparison.OrdinalIgnoreCase));
                        foreach (var fr in frameworkRefs)
                        {
                            var inc = fr.Attribute("Include")?.Value;
                            if (string.IsNullOrWhiteSpace(inc)) continue;
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references framework '{inc}'."));
                        }
                    }
                    catch { }

                    // Analyzer elements and package-based analyzers
                    try
                    {
                        var analyzerElems = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "Analyzer", StringComparison.OrdinalIgnoreCase));
                        foreach (var ae in analyzerElems)
                        {
                            var inc = ae.Attribute("Include")?.Value ?? ae.Value;
                            if (string.IsNullOrWhiteSpace(inc)) continue;
                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references analyzer '{inc}'."));
                        }

                        // Detect analyzer packages among package references
                        foreach (var pkg in discoveredPackages)
                        {
                            var lp = pkg.ToLowerInvariant();
                            if (lp.Contains("microsoft.codeanalysis") || lp.Contains("stylecop") || lp.Contains("analyzers") || lp.Contains("fxcop") )
                            {
                                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' references analyzer package '{pkg}'."));
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

                        // Namespace discovery extracted to NamespaceDiscoveryStep (ED-134)
                        // The NamespaceDiscoveryStep will be invoked after project discovery and will emit the same findings.
                        try { } catch { }

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
                                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' configures custom configuration providers."));
                                        }
                                    }
                                }
                                catch { }
                            }
                            catch { }
                        }
                        catch { }

                        // Type discovery: scan .cs files for declared types (class, interface, record, struct, enum, delegate)
                        try
                        {
                            var classCount = 0;
                            var interfaceCount = 0;
                            var recordCount = 0;
                            var structCount = 0;
                            var enumCount = 0;
                            var delegateCount = 0;
                            var constructorCount = 0;
                            var methodCount = 0;
                            var propertyCount = 0;
                            var fieldCount = 0;
                            var eventCount = 0;

                            if (!string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder))
                            {
                                var csFilesAll = Directory.GetFiles(projectFolder, "*.cs", SearchOption.AllDirectories);
                                var typeRegex = new Regex("\\b(class|interface|record(?:\\s+class|\\s+struct)?|struct|enum|delegate)\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
                                var nsRegexPerFile = new Regex("\\bnamespace\\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);

                                foreach (var csf in csFilesAll)
                                {
                                    try
                                    {
                                        var text = File.ReadAllText(csf);
                                        var nsMatch = nsRegexPerFile.Match(text);
                                        var fileNs = nsMatch.Success ? nsMatch.Groups[1].Value.Trim() : (rootNamespace ?? string.Empty);
                                        var matches = typeRegex.Matches(text);

                                        string ExtractBalancedBlock(string source, int startIndex)
                                        {
                                            var openIndex = source.IndexOf('{', startIndex);
                                            if (openIndex < 0) return string.Empty;
                                            var depth = 1;
                                            for (var i = openIndex + 1; i < source.Length; i++)
                                            {
                                                var ch = source[i];
                                                if (ch == '{') depth++;
                                                else if (ch == '}') depth--;
                                                if (depth == 0)
                                                {
                                                    return source.Substring(openIndex, i - openIndex + 1);
                                                }
                                            }
                                            return string.Empty;
                                        }

                                        foreach (Match m in matches)
                                        {
                                            var kindRaw = m.Groups[1].Value.Trim();
                                            var typeName = m.Groups[2].Value.Trim();
                                            string kind;
                                            if (kindRaw.StartsWith("record", StringComparison.OrdinalIgnoreCase)) kind = "record";
                                            else kind = kindRaw.ToLowerInvariant();

                                            switch (kind)
                                            {
                                                case "class": classCount++; totalClasses++; break;
                                                case "interface": interfaceCount++; totalInterfaces++; break;
                                                case "record": recordCount++; totalRecords++; break;
                                                case "struct": structCount++; totalStructs++; break;
                                                case "enum": enumCount++; totalEnums++; break;
                                                case "delegate": delegateCount++; totalDelegates++; break;
                                            }

                                            var fileNameOnly = Path.GetFileName(csf);
                                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' defines {kind} '{typeName}' in namespace '{(string.IsNullOrWhiteSpace(fileNs) ? "<global>" : fileNs)}' (file: {fileNameOnly})."));

                                            // Detect members inside the current type body only (balanced-brace extraction)
                                            try
                                            {
                                                var typeBody = ExtractBalancedBlock(text, m.Index);
                                                if (!string.IsNullOrWhiteSpace(typeBody))
                                                {
                                                    // Constructors
                                                    var ctorRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+" + Regex.Escape(typeName) + "\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
                                                    foreach (Match cm in ctorRegex.Matches(typeBody))
                                                    {
                                                        constructorCount++;
                                                        totalConstructors++;
                                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' defines constructor '{typeName}' in type '{typeName}'."));
                                                    }

                                                    // Methods
                                                    var methodRegexLocal = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|virtual\\s+|override\\s+|async\\s+|sealed\\s+|new\\s+|partial\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
                                                    foreach (Match mm in methodRegexLocal.Matches(typeBody))
                                                    {
                                                        var methodName = mm.Groups[4].Value.Trim();
                                                        if (string.IsNullOrWhiteSpace(methodName)) continue;
                                                        if (string.Equals(methodName, typeName, StringComparison.OrdinalIgnoreCase)) continue;
                                                        methodCount++;
                                                        totalMethods++;
                                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' defines method '{methodName}' in type '{typeName}'."));
                                                    }

                                                    // Properties
                                                    var propertyRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|virtual\\s+|override\\s+|sealed\\s+|new\\s+|partial\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\{\\s*(get\\s*;|set\\s*;|init\\s*;|get\\s*\\{|set\\s*\\{|init\\s*\\{)", RegexOptions.Compiled | RegexOptions.Multiline);
                                                    foreach (Match pm in propertyRegex.Matches(typeBody))
                                                    {
                                                        var propertyName = pm.Groups[4].Value.Trim();
                                                        if (string.IsNullOrWhiteSpace(propertyName)) continue;
                                                        propertyCount++;
                                                        totalProperties++;
                                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' defines property '{propertyName}' in type '{typeName}'."));
                                                    }

                                                    // Fields
                                                    var fieldRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|readonly\\s+|const\\s+|volatile\\s+|new\\s+)*[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*(=|;)", RegexOptions.Compiled | RegexOptions.Multiline);
                                                    foreach (Match fm in fieldRegex.Matches(typeBody))
                                                    {
                                                        var fieldName = fm.Groups[4].Value.Trim();
                                                        if (string.IsNullOrWhiteSpace(fieldName)) continue;
                                                        fieldCount++;
                                                        totalFields++;
                                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' defines field '{fieldName}' in type '{typeName}'."));
                                                    }

                                                    // Events
                                                    var eventRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+event\\s+[A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?]*\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled | RegexOptions.Multiline);
                                                    foreach (Match em in eventRegex.Matches(typeBody))
                                                    {
                                                        var eventName = em.Groups[3].Value.Trim();
                                                        if (string.IsNullOrWhiteSpace(eventName)) continue;
                                                        eventCount++;
                                                        totalEvents++;
                                                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' defines event '{eventName}' in type '{typeName}'."));
                                                    }
                                                }
                                            }
                                            catch { }
                                        }
                                    }
                                    catch { }
                                }
                            }

                            inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{name}' contains {classCount} classes, {interfaceCount} interfaces, {recordCount} records, {structCount} structs, {enumCount} enums, {delegateCount} delegates, {constructorCount} constructors, {methodCount} methods, {propertyCount} properties, {fieldCount} fields, {eventCount} events."));
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
                                var sourceDir = Path.GetDirectoryName(projFile) ?? throw new InvalidOperationException("Project file path must be rooted or within a discovered solution directory.");
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

            // Dependency graph discovery: build project dependency graph using ProjectReference elements
            try
            {
                // Map project full path -> name for quick lookup
                var pathToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in discoveredProjects)
                {
                    try
                    {
                        var full = Path.GetFullPath(p.Path ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(full) && !pathToName.ContainsKey(full)) pathToName[full] = p.Name ?? Path.GetFileNameWithoutExtension(p.Path) ?? full;
                    }
                    catch { }
                }

                // Build adjacency list (source -> list of referenced project names)
                var adjacency = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in discoveredProjects)
                {
                    var sourceName = p.Name ?? Path.GetFileNameWithoutExtension(p.Path) ?? "Unnamed";
                    if (!adjacency.ContainsKey(sourceName)) adjacency[sourceName] = new List<string>();
                    try
                    {
                        if (string.IsNullOrWhiteSpace(p.Path) || !File.Exists(p.Path)) continue;
                        var doc = XDocument.Load(p.Path);
                        var projectReferences = doc.Descendants().Where(x => string.Equals(x.Name.LocalName, "ProjectReference", StringComparison.OrdinalIgnoreCase));
                        var sourceDir = Path.GetDirectoryName(p.Path) ?? Directory.GetCurrentDirectory();
                        foreach (var pr in projectReferences)
                        {
                            try
                            {
                                var includeAttr = pr.Attribute("Include")?.Value;
                                if (string.IsNullOrWhiteSpace(includeAttr)) continue;
                                var referencedPath = includeAttr;
                                if (!Path.IsPathRooted(referencedPath)) referencedPath = Path.GetFullPath(Path.Combine(sourceDir, referencedPath));
                                else referencedPath = Path.GetFullPath(referencedPath);

                                var referencedName = pathToName.ContainsKey(referencedPath) ? pathToName[referencedPath] : Path.GetFileNameWithoutExtension(referencedPath) ?? referencedPath;
                                if (!string.Equals(sourceName, referencedName, StringComparison.OrdinalIgnoreCase) && !adjacency[sourceName].Contains(referencedName))
                                {
                                    adjacency[sourceName].Add(referencedName);
                                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{sourceName}' depends on project '{referencedName}'."));
                                }
                            }
                            catch { }
                        }
                    }
                    catch { }
                }

                // Ensure all discovered projects appear in adjacency (even if they have no outgoing refs)
                foreach (var p in discoveredProjects)
                {
                    var name = p.Name ?? Path.GetFileNameWithoutExtension(p.Path) ?? "Unnamed";
                    if (!adjacency.ContainsKey(name)) adjacency[name] = new List<string>();
                }

                // Compute incoming counts
                var incoming = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var key in adjacency.Keys) incoming[key] = 0;
                foreach (var kv in adjacency)
                {
                    foreach (var dep in kv.Value)
                    {
                        if (!incoming.ContainsKey(dep)) incoming[dep] = 0;
                        incoming[dep]++;
                    }
                }

                // Emit counts and standalone findings
                foreach (var projName in adjacency.Keys)
                {
                    var outCount = adjacency[projName].Count;
                    var inCount = incoming.ContainsKey(projName) ? incoming[projName] : 0;

                    if (outCount == 0)
                    {
                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{projName}' has no project dependencies."));
                    }
                    else
                    {
                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{projName}' has {outCount} outgoing project dependencies."));
                    }

                    if (inCount == 0)
                    {
                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{projName}' is not referenced by any project."));
                    }
                    else
                    {
                        inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Project '{projName}' has {inCount} incoming project dependencies."));
                    }
                }

                // Detect cycles using DFS and record unique cycles
                var cycles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var tempStack = new Stack<string>();
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                void Dfs(string node, HashSet<string> onStack)
                {
                    if (onStack.Contains(node)) return;
                    onStack.Add(node);
                    tempStack.Push(node);
                    if (adjacency.TryGetValue(node, out var nbrs))
                    {
                        foreach (var n in nbrs)
                        {
                            if (tempStack.Contains(n))
                            {
                                var arr = tempStack.Reverse().ToArray();
                                var idx = Array.IndexOf(arr, n);
                                if (idx >= 0)
                                {
                                    var cycle = arr.Take(idx + 1).Reverse().ToArray();
                                    var cycleKey = string.Join("->", cycle);
                                    if (!cycles.Contains(cycleKey))
                                    {
                                        cycles.Add(cycleKey);
                                    }
                                }
                            }
                            else if (!visited.Contains(n))
                            {
                                Dfs(n, onStack);
                            }
                        }
                    }
                    tempStack.Pop();
                    onStack.Remove(node);
                    visited.Add(node);
                }

                foreach (var n in adjacency.Keys)
                {
                    try { Dfs(n, new HashSet<string>(StringComparer.OrdinalIgnoreCase)); } catch { }
                }

                foreach (var c in cycles)
                {
                    var nodes = c.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                    var cyc = string.Join(" -> ", nodes) + " -> " + nodes.First();
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Circular dependency detected: {cyc}"));
                }

                // Determine the longest dependency chain (longest simple path)
                List<string> bestPath = new List<string>();

                List<string> DfsLongest(string node, HashSet<string> visitedLocal)
                {
                    if (visitedLocal.Contains(node)) return new List<string>();
                    visitedLocal.Add(node);
                    List<string> best = new List<string> { node };
                    if (adjacency.TryGetValue(node, out var nbrs))
                    {
                        foreach (var nb in nbrs)
                        {
                            var path = DfsLongest(nb, new HashSet<string>(visitedLocal));
                            if (path.Count + 1 > best.Count)
                            {
                                var newPath = new List<string> { node };
                                newPath.AddRange(path);
                                best = newPath;
                            }
                        }
                    }
                    return best;
                }

                foreach (var n in adjacency.Keys)
                {
                    try
                    {
                        var p = DfsLongest(n, new HashSet<string>());
                        if (p.Count > bestPath.Count) bestPath = p;
                    }
                    catch { }
                }

                if (bestPath.Count > 1)
                {
                    var chainText = string.Join(Environment.NewLine + "↓" + Environment.NewLine, bestPath);
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Longest dependency chain found:{Environment.NewLine}{chainText}"));
                }
            }
            catch { }

            // Solution Profile Discovery: summarize previously discovered findings into a concise solution profile
            try
            {
                var all = inv.Findings.Select(f => f.Description).ToList();

                // Solution overview
                var totalProjects = discoveredProjects.Count;
                var executableCount = all.Count(d => d.IndexOf("is an executable application", StringComparison.OrdinalIgnoreCase) >= 0
                    || d.IndexOf("appears to be a Console Application", StringComparison.OrdinalIgnoreCase) >= 0);
                var classLibraryCount = all.Count(d => d.IndexOf("is a class library", StringComparison.OrdinalIgnoreCase) >= 0
                    || d.IndexOf("appears to be a Class Library", StringComparison.OrdinalIgnoreCase) >= 0);
                var testCount = all.Count(d => d.IndexOf("Test Project", StringComparison.OrdinalIgnoreCase) >= 0
                    || d.IndexOf("appears to be a Test Project", StringComparison.OrdinalIgnoreCase) >= 0);

                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalProjects} projects."));
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {executableCount} executable projects."));
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {classLibraryCount} class libraries."));
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {testCount} test projects."));

                // Technologies summary (basic keywords)
                if (all.Any(d => d.IndexOf("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase) >= 0
                                  || d.IndexOf("uses the ASP.NET Core minimal hosting model", StringComparison.OrdinalIgnoreCase) >= 0
                                  || d.IndexOf("references framework 'Microsoft.AspNetCore.App'", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution contains ASP.NET Core projects."));
                }

                if (all.Any(d => d.IndexOf("references SignalR packages", StringComparison.OrdinalIgnoreCase) >= 0
                                  || d.IndexOf("references Microsoft.AspNetCore.SignalR", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution contains SignalR packages."));
                }

                if (all.Any(d => d.IndexOf("Entity Framework Core", StringComparison.OrdinalIgnoreCase) >= 0
                                  || d.IndexOf("references Entity Framework Core packages", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution contains Entity Framework Core packages."));
                }

                if (all.Any(d => d.IndexOf("OpenTelemetry", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution contains OpenTelemetry packages."));
                }

                if (all.Any(d => d.IndexOf("Serilog", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution contains Serilog packages."));
                }

                // Frameworks and SDKs summary
                var frameworks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in all.Where(x => x.IndexOf("targets ", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    var idx = d.IndexOf("targets ", StringComparison.OrdinalIgnoreCase);
                    var val = d.Substring(idx + "targets ".Length).Trim().TrimEnd('.');
                    if (!string.IsNullOrWhiteSpace(val)) frameworks.Add(val);
                }
                if (frameworks.Count > 0)
                {
                    var list = string.Join(", ", frameworks);
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution targets {list}."));
                }

                var sdks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var d in all.Where(x => x.IndexOf("uses ", StringComparison.OrdinalIgnoreCase) >= 0 && x.IndexOf("Microsoft.NET.Sdk", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    // description like "Name uses Microsoft.NET.Sdk.Web."
                    var parts = d.Split(new[] { " uses " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1) sdks.Add(parts[1].Trim().TrimEnd('.'));
                }
                if (sdks.Count > 0)
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution uses SDK(s): {string.Join(", ", sdks)}."));
                }

                // Type summary across solution
                try
                {
                    if (totalClasses > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalClasses} classes."));
                    if (totalInterfaces > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalInterfaces} interfaces."));
                    if (totalRecords > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalRecords} records."));
                    if (totalStructs > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalStructs} structs."));
                    if (totalEnums > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalEnums} enums."));
                    if (totalDelegates > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalDelegates} delegates."));
                    if (totalConstructors > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalConstructors} constructors."));
                    if (totalMethods > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalMethods} methods."));
                    if (totalProperties > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalProperties} properties."));
                    if (totalFields > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalFields} fields."));
                    if (totalEvents > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalEvents} events."));
                    var totalMembers = totalConstructors + totalMethods + totalProperties + totalFields + totalEvents;
                    if (totalMembers > 0) inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {totalMembers} members (constructors, methods, properties, fields, events)."));
                }
                catch { }

                // Configuration summary
                if (all.Any(d => d.IndexOf("contains appsettings", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution contains application configuration files (appsettings.json variants)."));
                }
                if (all.Any(d => d.IndexOf("launchSettings.json", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution contains launch profiles (launchSettings.json)."));
                }
                if (all.Any(d => d.IndexOf("User Secrets identifier", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, "Solution uses User Secrets."));
                }

                // Dependency summary
                var directDeps = all.Count(d => d.IndexOf("depends on project", StringComparison.OrdinalIgnoreCase) >= 0);
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, $"Solution contains {directDeps} direct project dependencies."));

                var hasCycles = all.Any(d => d.IndexOf("Circular dependency detected", StringComparison.OrdinalIgnoreCase) >= 0);
                inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, hasCycles ? "Solution contains circular project dependencies." : "Solution contains no circular project dependencies."));

                var longest = all.FirstOrDefault(d => d.IndexOf("Longest dependency chain found:", StringComparison.OrdinalIgnoreCase) >= 0);
                if (!string.IsNullOrWhiteSpace(longest))
                {
                    inv.AddFinding(new Finding(Guid.NewGuid(), FindingType.Observation, longest));
                }
            }
            catch { }

            return inv;
        }

        private static string? FindSolutionInParents()
        {
            try
            {
                // Note: Do not use AppContext.BaseDirectory or Directory.GetCurrentDirectory() to find repository.
                // This helper remains for legacy upward search but is no longer used by CreateInvestigation when repositoryRoot is required.
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

        // Find solutions under a repository root honoring exclusions
        public string[] FindSolutionsInRepository(string repositoryRoot)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(repositoryRoot) || !Directory.Exists(repositoryRoot)) return Array.Empty<string>();
                var ctx = new DiscoveryContext(null);
                var files = Directory.GetFiles(repositoryRoot, "*.sln*", SearchOption.AllDirectories)
                    .Where(f => !ctx.IsExcludedPath(f)).ToArray();
                return files;
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
