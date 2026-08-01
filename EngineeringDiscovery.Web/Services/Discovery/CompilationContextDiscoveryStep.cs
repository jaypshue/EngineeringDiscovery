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
                        foreach (var t in c.Types)
                        {
                        try
                        {
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
                                ImplementedInterfaces = t.ImplementedInterfaces ?? new System.Collections.Generic.List<string>(),
                                MethodCount = t.MethodCount,
                                ConstructorCount = t.ConstructorCount,
                                PropertyCount = t.PropertyCount,
                                FieldCount = t.FieldCount,
                                EventCount = t.EventCount,
                                PublicMemberCount = 0,
                                PrivateMemberCount = 0,
                                MemberCount = t.MethodCount + t.PropertyCount + t.FieldCount + t.EventCount + t.ConstructorCount
                            };

                            // Preserve member-level type references discovered by repository provider
                            try
                            {
                                if (t.ConstructorParameterTypes != null) typeObs.ConstructorParameterTypes.AddRange(t.ConstructorParameterTypes);
                                if (t.MethodParameterTypes != null) typeObs.MethodParameterTypes.AddRange(t.MethodParameterTypes);
                                if (t.FieldTypes != null) typeObs.FieldTypes.AddRange(t.FieldTypes);
                                if (t.PropertyTypes != null) typeObs.PropertyTypes.AddRange(t.PropertyTypes);
                                if (t.EventTypes != null) typeObs.EventTypes.AddRange(t.EventTypes);
                                if (t.GenericArgumentTypes != null) typeObs.GenericArgumentTypes.AddRange(t.GenericArgumentTypes);
                            }
                            catch { }

                            try { context.TypeObservations.Add(typeObs); } catch { }
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
