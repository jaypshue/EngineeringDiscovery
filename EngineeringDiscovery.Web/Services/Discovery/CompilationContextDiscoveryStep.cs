using System;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;
using EngineeringDiscovery.Web.Services.RepositoryLoading;
using System.Linq;

namespace EngineeringDiscovery.Web.Services.Discovery
{
    internal class CompilationContextDiscoveryStep : IInvestigationStep
    {
        private readonly Investigation _inv;

        public CompilationContextDiscoveryStep(Investigation inv)
        {
            _inv = inv ?? throw new ArgumentNullException(nameof(inv));
        }

        public InvestigationPhase Phase => InvestigationPhase.Discovery;

        public void Execute(InvestigationContext context)
        {
            if (context == null) return;

            try
            {
                var contexts = context.CompilationContexts;
                foreach (var c in contexts)
                {
                    try
                    {
                        try
                        {
                            foreach (var nsObs in c.NamespaceObservations)
                            {
                                try { context.NamespaceObservations.Add(nsObs); } catch { }
                                try { _inv.AddNamespaceObservation(nsObs); } catch { }
                            }
                        }
                        catch { }

                        foreach (var t in c.Types)
                        {
                        try
                        {
                            // Build a local display->qualified lookup for this compilation to enable producing
                            // canonical TypeReference entries where possible.
                            // Note: this map is scoped to the current compilation context and is used only
                            // to resolve references discovered in TypeDescriptor into canonical QualifiedName
                            // values for TypeReference objects.
                            var localDisplayToQualified = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            try
                            {
                                foreach (var tt in c.Types)
                                {
                                    try
                                    {
                                        var q = tt.QualifiedName ?? string.Empty;
                                        if (string.IsNullOrWhiteSpace(q)) continue;
                                        if (!string.IsNullOrWhiteSpace(tt.TypeName))
                                        {
                                            if (!localDisplayToQualified.ContainsKey(tt.TypeName)) localDisplayToQualified[tt.TypeName] = q;
                                            var nsKey = $"{tt.Namespace}.{tt.TypeName}";
                                            if (!localDisplayToQualified.ContainsKey(nsKey)) localDisplayToQualified[nsKey] = q;
                                        }
                                    }
                                    catch { }
                                }
                            }
                            catch { }

                            var typeObs = new EngineeringDiscovery.Core.Models.TypeObservation
                            {
                                Project = c.ProjectName,
                                Namespace = t.Namespace ?? string.Empty,
                                TypeName = t.TypeName,
                                QualifiedName = t.QualifiedName,
                                Kind = MapKind(t.Kind),
                                Accessibility = MapAccessibility(t.Accessibility),
                                IsAbstract = t.IsAbstract,
                                IsStatic = t.IsStatic,
                                IsPartial = false, // language-specific concept removed from contract; default conservative value
                                IsGeneric = t.IsGeneric,
                                GenericParameterCount = t.GenericParameterCount,
                                BaseType = t.BaseType,
                                ImplementedInterfaceCount = t.ImplementedInterfaceCount,
                                ImplementedInterfaces = new System.Collections.Generic.List<TypeReference>(),
                                MethodCount = t.MethodCount,
                                ConstructorCount = t.ConstructorCount,
                                PropertyCount = t.PropertyCount,
                                FieldCount = t.FieldCount,
                                EventCount = t.EventCount,
                                PublicMemberCount = 0,
                                PrivateMemberCount = 0,
                                MemberCount = t.MethodCount + t.PropertyCount + t.FieldCount + t.EventCount + t.ConstructorCount
                            };

                            // Convert discovery-provided display strings into canonical TypeReference objects
                            try
                            {
                                // BaseTypeReference
                                try
                                {
                                    if (!string.IsNullOrWhiteSpace(t.BaseType))
                                    {
                                        var display = t.BaseType!;
                                        var qn = localDisplayToQualified.TryGetValue(display, out var found) ? found : string.Empty;
                                        typeObs.BaseTypeReference = new TypeReference { DisplayName = display, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type };
                                    }
                                }
                                catch { }

                                // Implemented interfaces
                                try
                                {
                                    if (t.ImplementedInterfaces != null)
                                    {
                                        foreach (var disp in t.ImplementedInterfaces.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var qn = localDisplayToQualified.TryGetValue(disp, out var f) ? f : string.Empty;
                                                typeObs.ImplementedInterfaces.Add(new TypeReference { DisplayName = disp, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type });
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }

                                // Constructor parameter types
                                try
                                {
                                    if (t.ConstructorParameterTypes != null)
                                    {
                                        foreach (var disp in t.ConstructorParameterTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var qn = localDisplayToQualified.TryGetValue(disp, out var f) ? f : string.Empty;
                                                typeObs.ConstructorParameterTypes.Add(new TypeReference { DisplayName = disp, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type });
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }

                                // Method parameter types
                                try
                                {
                                    if (t.MethodParameterTypes != null)
                                    {
                                        foreach (var disp in t.MethodParameterTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var qn = localDisplayToQualified.TryGetValue(disp, out var f) ? f : string.Empty;
                                                typeObs.MethodParameterTypes.Add(new TypeReference { DisplayName = disp, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type });
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }

                                // Field types
                                try
                                {
                                    if (t.FieldTypes != null)
                                    {
                                        foreach (var disp in t.FieldTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var qn = localDisplayToQualified.TryGetValue(disp, out var f) ? f : string.Empty;
                                                typeObs.FieldTypes.Add(new TypeReference { DisplayName = disp, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type });
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }

                                // Property types
                                try
                                {
                                    if (t.PropertyTypes != null)
                                    {
                                        foreach (var disp in t.PropertyTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var qn = localDisplayToQualified.TryGetValue(disp, out var f) ? f : string.Empty;
                                                typeObs.PropertyTypes.Add(new TypeReference { DisplayName = disp, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type });
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }

                                // Event types
                                try
                                {
                                    if (t.EventTypes != null)
                                    {
                                        foreach (var disp in t.EventTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var qn = localDisplayToQualified.TryGetValue(disp, out var f) ? f : string.Empty;
                                                typeObs.EventTypes.Add(new TypeReference { DisplayName = disp, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type });
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }

                                // Generic argument types
                                try
                                {
                                    if (t.GenericArgumentTypes != null)
                                    {
                                        foreach (var disp in t.GenericArgumentTypes.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                        {
                                            try
                                            {
                                                var qn = localDisplayToQualified.TryGetValue(disp, out var f) ? f : string.Empty;
                                                typeObs.GenericArgumentTypes.Add(new TypeReference { DisplayName = disp, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.GenericArgument });
                                            }
                                            catch { }
                                        }
                                    }
                                }
                                catch { }
                            }
                            catch { }

                            // Map provider MemberDescriptors into MemberObservation and canonicalize member-level types
                            try
                            {
                                if (c.MemberDescriptors != null)
                                {
                                    var membersForType = c.MemberDescriptors.Where(md => string.Equals(md.TypeName ?? string.Empty, t.TypeName ?? string.Empty, StringComparison.OrdinalIgnoreCase));
                                    foreach (var md in membersForType)
                                    {
                                        try
                                        {
                                            var mo = new EngineeringDiscovery.Core.Models.MemberObservation
                                            {
                                                Project = md.Project,
                                                Namespace = string.IsNullOrWhiteSpace(md.Namespace) ? null : md.Namespace,
                                                Type = md.TypeName,
                                                MemberName = md.MemberName,
                                                Visibility = md.Visibility switch
                                                {
                                                    var s when string.Equals(s, "Public", StringComparison.OrdinalIgnoreCase) => EngineeringDiscovery.Core.Models.Visibility.Public,
                                                    var s when string.Equals(s, "Protected", StringComparison.OrdinalIgnoreCase) => EngineeringDiscovery.Core.Models.Visibility.Protected,
                                                    var s when string.Equals(s, "Internal", StringComparison.OrdinalIgnoreCase) => EngineeringDiscovery.Core.Models.Visibility.Internal,
                                                    var s when string.Equals(s, "Private", StringComparison.OrdinalIgnoreCase) => EngineeringDiscovery.Core.Models.Visibility.Private,
                                                    _ => EngineeringDiscovery.Core.Models.Visibility.Unknown
                                                },
                                                IsStatic = md.IsStatic,
                                                IsAsync = md.IsAsync,
                                                ReturnType = md.ReturnTypeDisplay,
                                                ReturnTypeReference = null,
                                                ParameterCount = md.ParameterTypeDisplays?.Count ?? 0,
                                                ParameterTypeReferences = new System.Collections.Generic.List<TypeReference>(),
                                                ApproximateSourceLines = md.LineCount
                                            };

                                            // Canonicalize return type
                                            try
                                            {
                                                if (!string.IsNullOrWhiteSpace(md.ReturnTypeDisplay))
                                                {
                                                    var display = md.ReturnTypeDisplay!;
                                                    var qn = localDisplayToQualified.TryGetValue(display, out var found) ? found : string.Empty;
                                                    mo.ReturnTypeReference = new TypeReference { DisplayName = display, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type };
                                                }
                                            }
                                            catch { }

                                            // Canonicalize parameter types
                                            try
                                            {
                                                if (md.ParameterTypeDisplays != null)
                                                {
                                                    foreach (var pd in md.ParameterTypeDisplays.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
                                                    {
                                                        try
                                                        {
                                                            var qn = localDisplayToQualified.TryGetValue(pd, out var f) ? f : string.Empty;
                                                            mo.ParameterTypeReferences.Add(new TypeReference { DisplayName = pd, QualifiedName = qn ?? string.Empty, IsExternal = string.IsNullOrWhiteSpace(qn), Kind = TypeReferenceKind.Type });
                                                        }
                                                        catch { }
                                                    }
                                                }
                                            }
                                            catch { }

                                            try { context.MemberObservations.Add(mo); } catch { }
                                            try { _inv.AddMemberObservation(mo); } catch { }
                                        }
                                        catch { }
                                    }
                                }
                            }
                            catch { }

                            try { context.TypeObservations.Add(typeObs); } catch { }
                            try { _inv.AddTypeObservation(typeObs); } catch { }
                        }
                            catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private EngineeringDiscovery.Core.Models.TypeKind MapKind(EngineeringTypeKind kind)
        {
            return kind switch
            {
                EngineeringTypeKind.Interface => EngineeringDiscovery.Core.Models.TypeKind.Interface,
                EngineeringTypeKind.Struct => EngineeringDiscovery.Core.Models.TypeKind.Struct,
                EngineeringTypeKind.Enum => EngineeringDiscovery.Core.Models.TypeKind.Enum,
                EngineeringTypeKind.Delegate => EngineeringDiscovery.Core.Models.TypeKind.Delegate,
                EngineeringTypeKind.Record => EngineeringDiscovery.Core.Models.TypeKind.Record,
                _ => EngineeringDiscovery.Core.Models.TypeKind.Class,
            };
        }

        private string MapAccessibility(EngineeringAccessibility a)
        {
            return a switch
            {
                EngineeringAccessibility.Public => "Public",
                EngineeringAccessibility.Internal => "Internal",
                EngineeringAccessibility.Protected => "Protected",
                EngineeringAccessibility.Private => "Private",
                EngineeringAccessibility.Package => "Package",
                _ => "Unknown",
            };
        }
    }
}
