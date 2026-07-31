using System;
using System.IO;
using System.Linq;
using EngineeringDiscovery.Core.Domain.Investigation;

namespace EngineeringDiscovery.Web.Services.ObservationEnrichment
{
    /// <summary>
    /// Deterministic normalization of raw TypeObservations produced by Discovery.
    /// This pass only normalizes formatting and canonicalizes vocabulary; it must not
    /// infer new facts or perform engineering judgment.
    /// </summary>
    internal class ObservationNormalizationPass : IObservationEnrichmentPass
    {
        public void Enrich(Investigation investigation)
        {
            if (investigation == null) return;

            try
            {
                var types = investigation.TypeObservations ?? Array.Empty<EngineeringDiscovery.Core.Models.TypeObservation>();
                foreach (var t in types)
                {
                    try
                    {
                        // Normalize null/empty strings
                        if (string.IsNullOrWhiteSpace(t.Namespace)) t.Namespace = "<Global>";
                        if (string.IsNullOrWhiteSpace(t.TypeName)) t.TypeName = "<Unknown>";

                        // Normalize QualifiedName: ensure deterministic formatting "Project:Namespace.TypeName" or "Namespace.TypeName"
                        if (string.IsNullOrWhiteSpace(t.QualifiedName))
                        {
                            if (!string.IsNullOrWhiteSpace(t.Project))
                            {
                                t.QualifiedName = !string.IsNullOrWhiteSpace(t.Namespace) && t.Namespace != "<Global>"
                                    ? $"{t.Project}:{t.Namespace}.{t.TypeName}"
                                    : $"{t.Project}:{t.TypeName}";
                            }
                            else
                            {
                                t.QualifiedName = !string.IsNullOrWhiteSpace(t.Namespace) && t.Namespace != "<Global>"
                                    ? $"{t.Namespace}.{t.TypeName}"
                                    : $"{t.TypeName}";
                            }
                        }
                        else
                        {
                            // Trim and normalize separators
                            t.QualifiedName = t.QualifiedName.Trim();
                        }

                        // Normalize Accessibility casing and canonical values
                        if (string.IsNullOrWhiteSpace(t.Accessibility)) t.Accessibility = "Unknown";
                        else
                        {
                            var a = t.Accessibility.Trim();
                            // Common canonical forms
                            if (string.Equals(a, "public", StringComparison.OrdinalIgnoreCase)) t.Accessibility = "Public";
                            else if (string.Equals(a, "private", StringComparison.OrdinalIgnoreCase)) t.Accessibility = "Private";
                            else if (string.Equals(a, "internal", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "package", StringComparison.OrdinalIgnoreCase)) t.Accessibility = "Internal";
                            else if (string.Equals(a, "protected", StringComparison.OrdinalIgnoreCase)) t.Accessibility = "Protected";
                            else t.Accessibility = char.ToUpperInvariant(a[0]) + (a.Length > 1 ? a.Substring(1) : string.Empty);
                        }

                        // (Do not modify source path on TypeObservation here; Discovery currently does not expose SourceFilePath.)

                        // Normalize numeric defaults: ensure non-negative
                        if (t.GenericParameterCount < 0) t.GenericParameterCount = 0;
                        if (t.MethodCount < 0) t.MethodCount = 0;
                        if (t.ConstructorCount < 0) t.ConstructorCount = 0;
                        if (t.PropertyCount < 0) t.PropertyCount = 0;
                        if (t.FieldCount < 0) t.FieldCount = 0;
                        if (t.EventCount < 0) t.EventCount = 0;
                        if (t.GenericParameterCount < 0) t.GenericParameterCount = 0;
                        if (t.ImplementsInterfaceCount < 0) t.ImplementsInterfaceCount = 0;
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
