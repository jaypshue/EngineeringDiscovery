using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading.FallbackParsing
{
    internal static class LooseFileMemberScanner
    {
        private static readonly Regex ConstructorRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex MethodRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+(static\\s+|virtual\\s+|override\\s+|async\\s+|sealed\\s+|new\\s+|partial\\s+)*([A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?\\s\\*]*)\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex FieldRegex = new Regex("(^|\\s)(public|private|protected|internal)\\s+([A-Za-z_][A-Za-z0-9_<>,\\[\\]\\.\\?\\s\\*]+)\\s+([A-Za-z_][A-Za-z0-9_,\\s]*)\\s*(=|;)", RegexOptions.Compiled | RegexOptions.Multiline);

        public static IEnumerable<(string TypeName, string MemberName, string Kind)> ScanMembersInType(string fileText, string typeName)
        {
            var results = new List<(string TypeName, string MemberName, string Kind)>();
            if (string.IsNullOrWhiteSpace(fileText) || string.IsNullOrWhiteSpace(typeName)) return results;

            // Constructors
            try
            {
                foreach (Match m in ConstructorRegex.Matches(fileText))
                {
                    var name = m.Groups[3].Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(name) && string.Equals(name, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add((typeName, name, "constructor"));
                    }
                }
            }
            catch { }

            // Methods
            try
            {
                foreach (Match m in MethodRegex.Matches(fileText))
                {
                    var memberName = m.Groups[4].Value?.Trim();
                    if (!string.IsNullOrWhiteSpace(memberName) && !string.Equals(memberName, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add((typeName, memberName, "method"));
                    }
                }
            }
            catch { }

            // Fields (simple capture)
            try
            {
                foreach (Match m in FieldRegex.Matches(fileText))
                {
                    var names = m.Groups[4].Value?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (names != null)
                    {
                        foreach (var n in names)
                        {
                            var nm = n.Trim();
                            if (!string.IsNullOrWhiteSpace(nm)) results.Add((typeName, nm, "field"));
                        }
                    }
                }
            }
            catch { }

            return results;
        }
    }
}
