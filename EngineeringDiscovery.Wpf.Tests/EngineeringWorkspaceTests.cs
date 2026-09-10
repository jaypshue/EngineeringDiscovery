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
            Assert.NotNull(typeof(EngineeringWorkspace));
        }

        [Fact]
        public void EngineeringWorkspace_XamlDefines_EngineOsHierarchyAndConversation()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            string? full = null;
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "EngineeringDiscovery.Wpf", "Views", "EngineeringWorkspace.xaml");
                if (File.Exists(candidate))
                {
                    full = candidate;
                    break;
                }
                directory = directory.Parent;
            }

            Assert.NotNull(full);
            var content = File.ReadAllText(full!);
            Assert.Contains("ENGINEOS / PROJECT HEADER", content);
            Assert.Contains("RepositoryName", content);
            Assert.Contains("EngineeringState.CurrentReadiness", content);
            Assert.Contains("Height=\"2*\"", content);
            Assert.Contains("MinHeight=\"300\"", content);
            Assert.Contains("MinHeight=\"0\"", content);
            Assert.Contains("Width=\"2.2*\"", content);
            Assert.Contains("PART_Conversation", content);
            Assert.Contains("CONVERSATION", content);
            Assert.Contains("Ask EngineOS about the project", content);
            Assert.Contains("Conversation.Messages", content);
            Assert.Contains("Conversation.StatusText", content);
            Assert.Contains("Conversation.IsBusy", content);
            Assert.Contains("Conversation.SendCommand", content);
            Assert.Contains("Conversation.Draft", content);
            Assert.Contains("Conversation.HasPendingConfirmation", content);
            Assert.Contains("Conversation.ConfirmActionCommand", content);
            Assert.Contains("Conversation.ConfirmationText", content);
            Assert.Contains("Content=\"Confirm\"", content);
            Assert.Contains("Explicit confirmation is required. Only the requested Build or Test operation will run", content);
            Assert.Contains("FontSize=\"14\"", content);
            Assert.Contains("MinHeight=\"52\"", content);
            Assert.Contains("ConversationMessageBorder", content);
            Assert.Contains("<views:EngineeringSteeringPanel Grid.Column=\"2\"", content);
            Assert.Contains("DataContext=\"{Binding DevelopmentSurface.Steering}\"", content);
            Assert.Contains("MaxWidth=\"300\"", content);
            Assert.Contains("MaxWidth=\"280\"", content);
            Assert.Contains("VerticalScrollBarVisibility=\"Auto\"", content);
            Assert.Contains("<views:DevelopmentSurface Grid.Row=\"2\" DataContext=\"{Binding DevelopmentSurface}\"", content);
            Assert.DoesNotContain("<ScrollViewer Grid.Row=\"2\"", content);
            Assert.DoesNotContain("CanContentScroll=\"False\"", content);
            Assert.DoesNotContain("<Expander", content);
            Assert.DoesNotContain("IsExpanded=\"False\"", content);
            Assert.DoesNotContain("Height=\"320\"", content);
            Assert.DoesNotContain("SurfaceBrush", content);
            Assert.DoesNotContain("EngineOSBackgroundBrush", content);
        }

        [Fact]
        public void MainWindow_Xaml_Defines_Responsive_Size_Floor()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            string? full = null;
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "EngineeringDiscovery.Wpf", "MainWindow.xaml");
                if (File.Exists(candidate))
                {
                    full = candidate;
                    break;
                }
                directory = directory.Parent;
            }

            Assert.NotNull(full);
            var content = File.ReadAllText(full!);
            Assert.Contains("MinWidth=\"900\"", content);
            Assert.Contains("MinHeight=\"600\"", content);
            Assert.Contains("x:Name=\"HostContent\"", content);
        }

        [Fact]
        public void EngineeringSteeringPanel_Xaml_PreservesAuthoritativeSteeringFields()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            string? full = null;
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "EngineeringDiscovery.Wpf", "Views", "EngineeringSteeringPanel.xaml");
                if (File.Exists(candidate))
                {
                    full = candidate;
                    break;
                }
                directory = directory.Parent;
            }

            Assert.NotNull(full);
            var content = File.ReadAllText(full!);
            Assert.Contains("ENGINEERING STEERING", content);
            Assert.Contains("CURRENT DIRECTION", content);
            Assert.Contains("CurrentDirection", content);
            Assert.Contains("CURRENT ROUND", content);
            Assert.Contains("CurrentRound", content);
            Assert.Contains("LAST COMPLETED", content);
            Assert.Contains("LastCompleted", content);
            Assert.Contains("NEXT HANDOFF", content);
            Assert.Contains("NextHandoff", content);
            Assert.Contains("CONFIDENCE", content);
            Assert.Contains("Confidence", content);
            Assert.Contains("HUMAN ATTENTION", content);
            Assert.Contains("HumanAttention", content);
            Assert.Contains("Generate Prompt", content);
            Assert.Contains("GeneratePromptCommand", content);
            Assert.DoesNotContain("Execute Round", content);
            Assert.DoesNotContain("SendMessageAsync", content);
        }

        [Fact]
        public void DevelopmentSurface_Xaml_PreservesSupportingEvidenceAndControls()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            string? full = null;
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "EngineeringDiscovery.Wpf", "Views", "DevelopmentSurface.xaml");
                if (File.Exists(candidate))
                {
                    full = candidate;
                    break;
                }
                directory = directory.Parent;
            }

            Assert.NotNull(full);
            var content = File.ReadAllText(full!);
            Assert.Contains("EVIDENCE / DEVELOPMENT SURFACE", content);
            Assert.Contains("Refresh evidence", content);
            Assert.Contains("Build", content);
            Assert.Contains("BuildCommand", content);
            Assert.Contains("Test", content);
            Assert.Contains("TestCommand", content);
            Assert.Contains("SelectedWorkbenchView", content);
            Assert.Contains("WorkbenchTabControl", content);
            Assert.Contains("WorkbenchTabItem", content);
            Assert.Contains("Trigger Property=\"IsSelected\"", content);
            Assert.Contains("FontWeight\" Value=\"Bold\"", content);
            Assert.Contains("Background\" Value=\"{StaticResource AccentBrush}\"", content);
            Assert.DoesNotContain("<RowDefinition Height=\"240\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Files}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Search}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Problems}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Changes}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Rounds}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Review}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Output}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Diff}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.Results}\"", content);
            Assert.Contains("Tag=\"{x:Static models:DevelopmentWorkbenchView.PromptArtifact}\"", content);
            Assert.Contains("File names", content);
            Assert.Contains("Source text", content);
            Assert.Contains("Header=\"Files\"", content);
            Assert.Contains("Header=\"Search\"", content);
            Assert.Contains("Header=\"Problems\"", content);
            Assert.Contains("Header=\"Changes\"", content);
            Assert.Contains("Header=\"Rounds\"", content);
            Assert.Contains("Header=\"Review\"", content);
            Assert.Contains("Header=\"Output\"", content);
            Assert.Contains("Header=\"Diff\"", content);
            Assert.Contains("Header=\"Results\"", content);
            Assert.Contains("Header=\"Prompt Artifact\"", content);
            Assert.Contains("DEVELOPMENT ROUNDS", content);
            Assert.Contains("PROMPT ARTIFACT", content);
            Assert.DoesNotContain("ENGINEERING STEERING", content);
            Assert.DoesNotContain("Generate Prompt", content);
            Assert.DoesNotContain("Execute Round", content);
        }
    }
}
