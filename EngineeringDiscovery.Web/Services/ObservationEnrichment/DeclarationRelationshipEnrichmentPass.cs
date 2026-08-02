using System;
using System.Collections.Generic;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    internal class DeclarationRelationshipEnrichmentPass : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                var types = (investigation.TypeObservations ?? Array.Empty<TypeObservation>())
                    .Where(type => !string.IsNullOrWhiteSpace(type.QualifiedName) || !string.IsNullOrWhiteSpace(type.TypeName))
                    .OrderBy(type => type.QualifiedName ?? type.TypeName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (types.Count == 0) return;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var type in types)
                {
                    var sourceQualifiedName = type.QualifiedName ?? type.TypeName ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(sourceQualifiedName)) continue;

                    AddRelationship(
                        investigation,
                        seen,
                        type,
                        type.BaseTypeReference,
                        RelationshipKind.Extends,
                        string.IsNullOrWhiteSpace(type.BaseType) ? string.Empty : $"Base type declaration: {type.BaseType}");

                    foreach (var interfaceReference in type.ImplementedInterfaces ?? Enumerable.Empty<TypeReference>())
                    {
                        AddRelationship(
                            investigation,
                            seen,
                            type,
                            interfaceReference,
                            RelationshipKind.Implements,
                            string.IsNullOrWhiteSpace(interfaceReference?.DisplayName) ? string.Empty : $"Implements declaration: {interfaceReference.DisplayName}");
                    }
                }
            }
            catch { }
        }

        private static void AddRelationship(
            Investigation investigation,
            HashSet<string> seen,
            TypeObservation source,
            TypeReference? target,
            RelationshipKind kind,
            string evidence)
        {
            if (target == null) return;

            var sourceQualifiedName = source.QualifiedName ?? source.TypeName ?? string.Empty;
            var targetDisplayName = target.DisplayName ?? string.Empty;
            var targetQualifiedName = target.QualifiedName ?? string.Empty;

            if (string.IsNullOrWhiteSpace(sourceQualifiedName)) return;
            if (string.IsNullOrWhiteSpace(targetDisplayName) && string.IsNullOrWhiteSpace(targetQualifiedName)) return;
            if (!string.IsNullOrWhiteSpace(targetQualifiedName) && string.Equals(sourceQualifiedName, targetQualifiedName, StringComparison.OrdinalIgnoreCase)) return;

            var identity = $"{kind}|{sourceQualifiedName}|{targetQualifiedName}|{targetDisplayName}";
            if (!seen.Add(identity)) return;

            investigation.AddRelationshipObservation(new RelationshipObservation
            {
                SourceProject = source.Project ?? string.Empty,
                SourceNamespace = source.Namespace ?? string.Empty,
                SourceType = source.TypeName ?? string.Empty,
                SourceQualifiedName = sourceQualifiedName,
                TargetDisplayName = targetDisplayName,
                TargetQualifiedName = targetQualifiedName,
                Kind = kind,
                IsExternal = target.IsExternal || string.IsNullOrWhiteSpace(targetQualifiedName),
                Evidence = evidence
            });
        }
    }
}
