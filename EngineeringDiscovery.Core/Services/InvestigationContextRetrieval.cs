using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using EngineeringDiscovery.Core.Domain.Investigation;
using EngineeringDiscovery.Core.Models;

namespace EngineeringDiscovery.Core.Services
{
    /// <summary>
    /// Retrieves focused engineering context from an Investigation based on a natural-language question.
    /// Produces a structured text summary suitable for LLM system prompts.
    /// </summary>
    public static class InvestigationContextRetrieval
    {
        /// <summary>
        /// Given a user question and an Investigation, retrieve relevant types, members, and relationships.
        /// Returns a formatted string for inclusion in an LLM system prompt.
        /// </summary>
        public static string RetrieveContextForQuestion(string question, Investigation investigation)
        {
            if (investigation == null || string.IsNullOrWhiteSpace(question))
                return string.Empty;

            var sb = new StringBuilder();

            // Repository-level metadata (always included, concise)
            var summary = InvestigationSummary.CreateFrom(investigation);
            sb.AppendLine($"\n\nENGINEERING MODEL: {summary.RepositoryName}");
            sb.AppendLine($"Structure: {summary.ProjectCount} projects, {summary.TypeCount} types, {summary.NamespaceCount} namespaces, {summary.MemberCount} members");
            sb.AppendLine();

            // Extract keywords from the question for matching
            var keywords = ExtractKeywords(question);

            // Find relevant types by keyword matching
            var allTypes = investigation.TypeObservations ?? Array.Empty<TypeObservation>();
            var relevantTypes = FindRelevantTypes(keywords, allTypes).ToList();

            if (relevantTypes.Count == 0)
            {
                // Fallback: include a broad sample if no keywords matched
                sb.AppendLine("No specific types matched the question. Available types:");
                sb.AppendLine(string.Join(", ", allTypes.Take(40).Select(t => t.TypeName)));
                return sb.ToString();
            }

            // For matched types, also pull in their direct dependencies (one hop)
            var relevantQualifiedNames = new HashSet<string>(relevantTypes.Select(t => t.QualifiedName ?? "").Where(q => !string.IsNullOrWhiteSpace(q)), StringComparer.OrdinalIgnoreCase);
            if (investigation.RelationshipGraph != null)
            {
                var additionalQNs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var qn in relevantQualifiedNames)
                {
                    foreach (var (type, target) in investigation.RelationshipGraph.GetOutgoingRelationships(qn))
                    {
                        additionalQNs.Add(target);
                    }
                }
                // Add dependency targets that aren't already in the set
                foreach (var qn in additionalQNs)
                {
                    var depType = allTypes.FirstOrDefault(t => string.Equals(t.QualifiedName, qn, StringComparison.OrdinalIgnoreCase));
                    if (depType != null && !relevantTypes.Contains(depType))
                    {
                        relevantTypes.Add(depType);
                        relevantQualifiedNames.Add(qn);
                    }
                }
            }

            // Cap to avoid prompt overflow
            if (relevantTypes.Count > 25) relevantTypes = relevantTypes.Take(25).ToList();

            // Format types with their members and relationships
            sb.AppendLine("RELEVANT ENGINEERING CONTEXT (from the imported engineering model):");
            sb.AppendLine();

            var allMembers = investigation.MemberObservations ?? Array.Empty<MemberObservation>();

            foreach (var type in relevantTypes)
            {
                sb.AppendLine($"## {type.Project}/{type.Namespace}.{type.TypeName} [{type.Kind}]");

                // Members for this type
                var typeMembers = allMembers.Where(m =>
                    string.Equals(m.Type, type.TypeName, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(m.Namespace, type.Namespace, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (typeMembers.Count > 0)
                {
                    sb.AppendLine("  Members:");
                    foreach (var m in typeMembers.Where(m => m.Visibility == Visibility.Public || m.MemberName == ".ctor"))
                    {
                        var asyncMark = m.IsAsync ? "async " : "";
                        var returnInfo = string.IsNullOrWhiteSpace(m.ReturnType) ? "" : $" → {m.ReturnType}";
                        sb.AppendLine($"    {m.Visibility.ToString().ToLower()} {asyncMark}{m.MemberName}({m.ParameterCount} params){returnInfo}");
                    }

                    // Private fields (show type-holding fields to reveal dependencies)
                    var fields = typeMembers.Where(m => m.Visibility == Visibility.Private && !string.IsNullOrWhiteSpace(m.ReturnType) && m.MemberName.StartsWith("_")).ToList();
                    if (fields.Count > 0)
                    {
                        sb.AppendLine("  Dependencies (injected fields):");
                        foreach (var f in fields)
                        {
                            sb.AppendLine($"    {f.MemberName}: {f.ReturnType}");
                        }
                    }
                }

                // Outgoing relationships
                if (investigation.RelationshipGraph != null && !string.IsNullOrWhiteSpace(type.QualifiedName))
                {
                    var outgoing = investigation.RelationshipGraph.GetOutgoingRelationships(type.QualifiedName).ToList();
                    if (outgoing.Count > 0)
                    {
                        sb.AppendLine("  Relationships:");
                        foreach (var (relType, target) in outgoing.Take(15))
                        {
                            // Shorten target for readability
                            var shortTarget = target.Contains(':') ? target.Split(':').Last() : target;
                            sb.AppendLine($"    --[{relType}]--> {shortTarget}");
                        }
                    }
                }

                sb.AppendLine();
            }

            // Add relevant findings
            if (investigation.Findings != null && investigation.Findings.Count > 0)
            {
                var relevantFindings = investigation.Findings
                    .Where(f => f.Description != null && keywords.Any(k => f.Description.Contains(k, StringComparison.OrdinalIgnoreCase)))
                    .Take(10)
                    .ToList();

                if (relevantFindings.Count > 0)
                {
                    sb.AppendLine("RELEVANT FINDINGS:");
                    foreach (var f in relevantFindings)
                        sb.AppendLine($"  - {f.Description}");
                    sb.AppendLine();
                }
            }

            // Brief instruction for the LLM on data provenance
            sb.AppendLine("DATA SOURCE: Static structural analysis. Dependencies = constructor injection references. Method call sequences require source inspection.");

            return sb.ToString();
        }

        /// <summary>
        /// Extract meaningful keywords from a natural-language question.
        /// Focuses on likely type/method/concept names.
        /// </summary>
        private static List<string> ExtractKeywords(string question)
        {
            var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "the", "a", "an", "is", "are", "was", "were", "how", "does", "do", "what", "where",
                "when", "which", "from", "to", "in", "of", "for", "and", "or", "that", "this",
                "with", "by", "on", "at", "it", "its", "be", "been", "being", "have", "has", "had",
                "get", "gets", "got", "through", "into", "then", "after", "before", "between",
                "about", "can", "could", "would", "should", "will", "trace", "show", "tell", "me",
                "explain", "describe", "find", "look", "up", "down", "all", "any", "each", "every",
                "my", "our", "your", "their", "i", "we", "you", "they", "not", "no", "yes",
                "also", "just", "only", "still", "already", "even", "if", "but", "so", "yet",
                "more", "most", "very", "much", "many", "some", "few", "other", "another",
                "new", "old", "first", "last", "next", "same", "different", "such", "like"
            };

            var words = question.Split(new[] { ' ', ',', '.', '?', '!', ':', ';', '(', ')', '[', ']', '{', '}', '"', '\'' }, StringSplitOptions.RemoveEmptyEntries);

            return words
                .Where(w => w.Length > 2 && !stopWords.Contains(w))
                .Select(w => w.TrimEnd('s')) // basic stemming: remove trailing 's'
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Find types relevant to the given keywords by matching against type name, namespace, and project.
        /// Uses fuzzy substring matching.
        /// </summary>
        private static IEnumerable<TypeObservation> FindRelevantTypes(List<string> keywords, IReadOnlyList<TypeObservation> allTypes)
        {
            var scored = new Dictionary<TypeObservation, int>();

            foreach (var type in allTypes)
            {
                int score = 0;
                foreach (var keyword in keywords)
                {
                    if (type.TypeName != null && type.TypeName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        score += 3; // Strong match on type name
                    else if (type.Namespace != null && type.Namespace.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        score += 1; // Weak match on namespace
                    else if (type.Project != null && type.Project.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                        score += 1; // Weak match on project
                }

                if (score > 0) scored[type] = score;
            }

            // Return top matches sorted by relevance
            return scored.OrderByDescending(kv => kv.Value).Take(15).Select(kv => kv.Key);
        }
    }
}
