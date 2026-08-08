using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace EngineeringDiscovery.Web.Services.RepositoryLoading.FallbackParsing
{
    internal static class LooseFileTypeScanner
    {
        private static readonly Regex TypeRegex = new Regex("\\b(class|interface|record(?:\\s+class|\\s+struct)?|struct|enum|delegate)\\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.Compiled);
        private static readonly Regex NamespaceRegex = new Regex("\\bnamespace\\s+([A-Za-z_][A-Za-z0-9_.]*)", RegexOptions.Compiled);

        public static IEnumerable<(string Namespace, string TypeName, string Kind, string FilePath)> ScanCsFile(string filePath)
        {
            var results = new List<(string Namespace, string TypeName, string Kind, string FilePath)>();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return results;
            string text;
            try { text = File.ReadAllText(filePath); } catch { return results; }

            string fileNs = string.Empty;
            try
            {
                var m = NamespaceRegex.Match(text);
                if (m.Success) fileNs = m.Groups[1].Value.Trim();
            }
            catch { }

            foreach (Match m in TypeRegex.Matches(text))
            {
                var kindRaw = m.Groups[1].Value.Trim();
                var typeName = m.Groups[2].Value.Trim();
                string kind;
                if (kindRaw.StartsWith("record", StringComparison.OrdinalIgnoreCase)) kind = "record";
                else kind = kindRaw.ToLowerInvariant();
                results.Add((fileNs, typeName, kind, filePath));
            }

            return results;
        }
    }
}
