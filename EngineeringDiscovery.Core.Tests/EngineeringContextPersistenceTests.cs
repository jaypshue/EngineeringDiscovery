using System;
using System.IO;
using EngineeringDiscovery.Core.Domain.CurrentTask;
using EngineeringDiscovery.Core.Domain.Workspace;
using EngineeringDiscovery.Web.Services;
using Xunit;

namespace EngineeringDiscovery.Core.Tests
{
    public class EngineeringContextPersistenceTests
    {
        [Fact]
        public void EngineeringContext_RoundTrips_Via_WorkspaceState()
        {
            // Arrange
            var ws1 = new WorkspaceState(null);

            var workspace = new Workspace
            {
                RepositoryPath = "C:\\temp\\repo"
            };

            var ct = new CurrentTask("T","D","G");
            ct.Brief.Context.AddProject("P1");
            ct.Brief.Context.AddNamespace("N1");
            ct.Brief.Context.AddType("T1");
            workspace.CurrentTask = ct;

            // Use reflection to set the private setter for ActiveWorkspace for testing
            var prop = typeof(WorkspaceState).GetProperty("ActiveWorkspace");
            Assert.NotNull(prop);
            prop!.SetValue(ws1, workspace);

            // Act
            ws1.Save();

            // Create a fresh WorkspaceState which will read the persisted file
            var ws2 = new WorkspaceState(null);

            // Assert
            Assert.NotNull(ws2.ActiveWorkspace);
            Assert.NotNull(ws2.ActiveWorkspace.CurrentTask);
            var restored = ws2.ActiveWorkspace.CurrentTask;
            Assert.Contains("P1", restored.Brief.Context.ProjectIds);
            Assert.Contains("N1", restored.Brief.Context.NamespaceIds);
            Assert.Contains("T1", restored.Brief.Context.TypeIds);

            // Cleanup persisted file
            try
            {
                var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var path = Path.Combine(localAppData, "EngineeringDiscovery", "workspace.json");
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }
    }
}
