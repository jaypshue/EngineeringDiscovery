using System;
using System.IO;
using Xunit;
using EngineeringDiscovery.Wpf.Views;

namespace EngineeringDiscovery.Wpf.Tests
{
    public class EngineeringWorkspaceTests
    {
        [Fact]
        public void EngineeringWorkspace_TypeExists()
        {
            // Ensure the control type is present in the WPF assembly
            Assert.NotNull(typeof(EngineeringWorkspace));
        }

        [Fact]
        public void EngineeringWorkspace_XamlDefines_Expected_Parts()
        {
            // Verify the XAML includes the named regions so UI tests can bind to them later
            var path = Path.Combine("..", "EngineeringDiscovery.Wpf", "Views", "EngineeringWorkspace.xaml");
            var full = Path.GetFullPath(path);
            Assert.True(File.Exists(full), $"Expected XAML file at {full}");
            var content = File.ReadAllText(full);
            Assert.Contains("PART_Conversation", content);
            Assert.Contains("PART_Context", content);
            Assert.Contains("PART_Evidence", content);
        }
    }
}
