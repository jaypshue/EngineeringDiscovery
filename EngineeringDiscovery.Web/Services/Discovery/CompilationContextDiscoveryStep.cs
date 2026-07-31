using System;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;
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
                                    Project = t.ProjectName,
                                    Namespace = t.Namespace ?? string.Empty,
                                    TypeName = t.TypeName,
                                    QualifiedName = t.QualifiedName,
                                    Kind = MapKind(t.Kind),
                                    Accessibility = t.Accessibility ?? string.Empty,
                                    IsAbstract = t.IsAbstract,
                                    IsStatic = t.IsStatic,
                                    IsPartial = t.IsPartial,
                                    IsGeneric = t.IsGeneric,
                                    GenericParameterCount = t.GenericParameterCount,
                                    BaseType = t.BaseType,
                                    ImplementedInterfaceCount = 0,
                                    MethodCount = t.MethodCount,
                                    ConstructorCount = t.ConstructorCount,
                                    PropertyCount = t.PropertyCount,
                                    FieldCount = t.FieldCount,
                                    EventCount = t.EventCount,
                                    PublicMemberCount = 0,
                                    PrivateMemberCount = 0,
                                    MemberCount = t.MethodCount + t.PropertyCount + t.FieldCount + t.EventCount + t.ConstructorCount
                                };

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

        private EngineeringDiscovery.Core.Models.TypeKind MapKind(string kind)
        {
            if (string.Equals(kind, "Interface", StringComparison.OrdinalIgnoreCase)) return EngineeringDiscovery.Core.Models.TypeKind.Interface;
            if (string.Equals(kind, "Struct", StringComparison.OrdinalIgnoreCase)) return EngineeringDiscovery.Core.Models.TypeKind.Struct;
            if (string.Equals(kind, "Enum", StringComparison.OrdinalIgnoreCase)) return EngineeringDiscovery.Core.Models.TypeKind.Enum;
            if (string.Equals(kind, "Delegate", StringComparison.OrdinalIgnoreCase)) return EngineeringDiscovery.Core.Models.TypeKind.Delegate;
            if (string.Equals(kind, "Record", StringComparison.OrdinalIgnoreCase)) return EngineeringDiscovery.Core.Models.TypeKind.Record;
            return EngineeringDiscovery.Core.Models.TypeKind.Class;
        }
    }
}
