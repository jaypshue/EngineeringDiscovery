using System.Collections.Generic;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading
{
    internal enum RepositoryLanguage
    {
        Unknown,
        CSharp,
        Java
    }

    internal enum JavaBuildSystem
    {
        Unknown,
        Maven,
        Gradle
    }

    internal class SourceRootDescriptor
    {
        public string Path { get; set; } = string.Empty;

        public bool IsTestSource { get; set; }
    }

    internal class JavaRepositoryLayout
    {
        public string RepositoryRoot { get; set; } = string.Empty;

        public JavaBuildSystem BuildSystem { get; set; } = JavaBuildSystem.Unknown;

        public List<string> Modules { get; } = new List<string>();

        public List<SourceRootDescriptor> SourceRoots { get; } = new List<SourceRootDescriptor>();

        public List<string> JavaSourceFiles { get; } = new List<string>();

        public int ModuleCount => Modules.Count;

        public int SourceRootCount => SourceRoots.Count;

        public int JavaFileCount => JavaSourceFiles.Count;
    }

    // Language-agnostic compilation context produced by repository providers.
    internal class CompilationContext
    {
        public RepositoryLanguage Language { get; set; } = RepositoryLanguage.Unknown;

        public string ProjectName { get; set; } = string.Empty;

        public string? ProjectFilePath { get; set; }

        public string RepositoryRoot { get; set; } = string.Empty;

        public JavaRepositoryLayout? JavaLayout { get; set; }

        public List<TypeDescriptor> Types { get; } = new List<TypeDescriptor>();

        // Member descriptors produced by repository providers (language-specific)
        public List<MemberDescriptor> MemberDescriptors { get; } = new List<MemberDescriptor>();
    }
}
