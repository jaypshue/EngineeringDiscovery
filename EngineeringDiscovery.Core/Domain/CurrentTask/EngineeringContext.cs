using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineeringDiscovery.Core.Domain.CurrentTask
{
    public sealed class EngineeringContext
    {
        public EngineeringContext()
        {
            ProjectIds = new List<string>();
            NamespaceIds = new List<string>();
            TypeIds = new List<string>();
        }

        public List<string> ProjectIds { get; }

        public List<string> NamespaceIds { get; }

        public List<string> TypeIds { get; }

        public void AddProject(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!ProjectIds.Contains(id)) ProjectIds.Add(id);
        }

        public void RemoveProject(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            ProjectIds.Remove(id);
        }

        public void AddNamespace(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!NamespaceIds.Contains(id)) NamespaceIds.Add(id);
        }

        public void RemoveNamespace(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            NamespaceIds.Remove(id);
        }

        public void AddType(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            if (!TypeIds.Contains(id)) TypeIds.Add(id);
        }

        public void RemoveType(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            TypeIds.Remove(id);
        }

        public bool IsEmpty() => !ProjectIds.Any() && !NamespaceIds.Any() && !TypeIds.Any();
    }
}
