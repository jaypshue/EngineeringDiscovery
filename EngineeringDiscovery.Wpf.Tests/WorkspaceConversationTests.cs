using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Wpf.Models;
using EngineeringDiscovery.Wpf.Services;
using EngineeringDiscovery.Wpf.ViewModels;

namespace EngineeringDiscovery.Wpf.Tests
{
    public class WorkspaceConversationTests
    {
        [Fact]
        public async Task SendMessage_Invokes_Partner_And_Appends_Messages()
        {
            var mock = new Mock<IEngineeringPartner>();
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            mock.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            mock.Setup(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync("Reply from partner");

            var vm = new WorkspaceConversationViewModel(mock.Object);
            await vm.InitializeAsync("start");

            vm.Draft = "Hello";
            var command = Assert.IsType<AsyncRelayCommand>(vm.SendCommand);
            command.Execute(null);
            Assert.NotNull(command.LastTask);
            await command.LastTask!;

            Assert.Contains(vm.Messages, m => m.Speaker == "You" && m.Text.Contains("Hello"));
            Assert.Contains(vm.Messages, m => m.Speaker == "EngineOS" && m.Text.Contains("Reply from partner"));
            Assert.False(vm.IsBusy);
            Assert.Equal("Ready", vm.StatusText);

            mock.Verify(p => p.StartSessionAsync(It.IsAny<string>()), Times.Once);
            mock.Verify(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), "Hello"), Times.Once);
        }

        [Fact]
        public async Task Structured_Confirmation_Request_Is_Rendered_Without_Executing()
        {
            var partner = new Mock<IEngineeringPartner>();
            var structured = new Mock<IStructuredConversationPartner>();
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            partner.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            structured.Setup(p => p.SendStructuredReadOnlyMessageAsync(
                    It.IsAny<Guid>(),
                    "Run the tests",
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StructuredConversationResult(
                    ConversationResponseKind.ConfirmationRequired,
                    "I can propose this action: Run the repository test operation. Confirmation required.",
                    "Run tests",
                    "Run the repository test operation",
                    Operation: EngineeringOperationKind.Test));

            var executor = new Mock<IConfirmedEngineeringOperationExecutor>();
            executor.Setup(e => e.ExecuteAsync(EngineeringOperationKind.Test, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevelopmentCommandResult(
                    DevelopmentCommandKind.Test,
                    "dotnet test",
                    0,
                    TimeSpan.FromSeconds(1),
                    Array.Empty<ProblemItem>(),
                    "test output"));

            var vm = new WorkspaceConversationViewModel(partner.Object, structured.Object, executor.Object);
            await vm.InitializeAsync();
            vm.Draft = "Run the tests";

            await vm.SendCurrentMessageAsync();

            Assert.True(vm.HasPendingConfirmation);
            Assert.Equal("Run the repository test operation", vm.PendingAction);
            var response = vm.Messages.Last(message => message.Speaker == "EngineOS");
            Assert.Equal(ConversationResponseKind.ConfirmationRequired, response.ResponseKind);
            Assert.Equal("Run the repository test operation", response.ProposedAction);
            partner.Verify(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

            executor.Verify(e => e.ExecuteAsync(EngineeringOperationKind.Test, It.IsAny<CancellationToken>()), Times.Never);

            var confirmCommand = Assert.IsType<AsyncRelayCommand>(vm.ConfirmActionCommand);
            confirmCommand.Execute(null);
            Assert.NotNull(confirmCommand.LastTask);
            await confirmCommand.LastTask!;

            Assert.False(vm.HasPendingConfirmation);
            Assert.Contains(vm.Messages, message =>
                message.Speaker == "EngineOS" &&
                message.Text.Contains("Tests completed successfully", StringComparison.OrdinalIgnoreCase));
            executor.Verify(e => e.ExecuteAsync(EngineeringOperationKind.Test, It.IsAny<CancellationToken>()), Times.Once);
            structured.Verify(p => p.SendStructuredReadOnlyMessageAsync(
                It.IsAny<Guid>(), "Run the tests", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Confirmed_Build_Uses_The_Shared_Operation_Executor()
        {
            var partner = new Mock<IEngineeringPartner>();
            var structured = new Mock<IStructuredConversationPartner>();
            var executor = new Mock<IConfirmedEngineeringOperationExecutor>();
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            partner.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            structured.Setup(p => p.SendStructuredReadOnlyMessageAsync(
                    It.IsAny<Guid>(), "Build it", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StructuredConversationResult(
                    ConversationResponseKind.ConfirmationRequired,
                    "Confirmation required.",
                    "Build",
                    "Run the repository build operation",
                    Operation: EngineeringOperationKind.Build));
            executor.Setup(e => e.ExecuteAsync(EngineeringOperationKind.Build, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevelopmentCommandResult(
                    DevelopmentCommandKind.Build,
                    "dotnet build",
                    1,
                    TimeSpan.FromSeconds(2),
                    Array.Empty<ProblemItem>(),
                    "build output"));

            var vm = new WorkspaceConversationViewModel(partner.Object, structured.Object, executor.Object);
            await vm.InitializeAsync();
            vm.Draft = "Build it";
            await vm.SendCurrentMessageAsync();

            executor.Verify(e => e.ExecuteAsync(EngineeringOperationKind.Build, It.IsAny<CancellationToken>()), Times.Never);

            var confirmCommand = Assert.IsType<AsyncRelayCommand>(vm.ConfirmActionCommand);
            confirmCommand.Execute(null);
            await confirmCommand.LastTask!;

            Assert.Contains(vm.Messages, message => message.Text.Contains("Build failed", StringComparison.OrdinalIgnoreCase));
            executor.Verify(e => e.ExecuteAsync(EngineeringOperationKind.Build, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("Build it", EngineeringOperationKind.Build, DevelopmentCommandKind.Build, 0, "Build completed successfully")]
        [InlineData("Run the tests", EngineeringOperationKind.Test, DevelopmentCommandKind.Test, 1, "Tests failed")]
        public async Task Confirmed_Build_And_Test_Results_Are_Reported(
            string request,
            EngineeringOperationKind operation,
            DevelopmentCommandKind commandKind,
            int exitCode,
            string expectedText)
        {
            var partner = new Mock<IEngineeringPartner>();
            var structured = new Mock<IStructuredConversationPartner>();
            var executor = new Mock<IConfirmedEngineeringOperationExecutor>();
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            partner.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            structured.Setup(p => p.SendStructuredReadOnlyMessageAsync(
                    It.IsAny<Guid>(), request, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StructuredConversationResult(
                    ConversationResponseKind.ConfirmationRequired,
                    "Confirmation required.",
                    operation == EngineeringOperationKind.Build ? "Build" : "Run tests",
                    operation == EngineeringOperationKind.Build
                        ? "Run the repository build operation"
                        : "Run the repository test operation",
                    Operation: operation));
            executor.Setup(e => e.ExecuteAsync(operation, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DevelopmentCommandResult(
                    commandKind,
                    operation == EngineeringOperationKind.Build ? "dotnet build" : "dotnet test",
                    exitCode,
                    TimeSpan.FromSeconds(1),
                    Array.Empty<ProblemItem>(),
                    "captured output"));

            var vm = new WorkspaceConversationViewModel(partner.Object, structured.Object, executor.Object);
            await vm.InitializeAsync();
            vm.Draft = request;
            await vm.SendCurrentMessageAsync();

            var confirmCommand = Assert.IsType<AsyncRelayCommand>(vm.ConfirmActionCommand);
            confirmCommand.Execute(null);
            await confirmCommand.LastTask!;

            Assert.Contains(vm.Messages, message => message.Text.Contains(expectedText, StringComparison.OrdinalIgnoreCase));
            Assert.Contains(vm.Messages, message =>
                message.Text.Contains("Status:", StringComparison.OrdinalIgnoreCase) &&
                message.Text.Contains("exit code", StringComparison.OrdinalIgnoreCase) &&
                message.Text.Contains("elapsed", StringComparison.OrdinalIgnoreCase));
            executor.Verify(e => e.ExecuteAsync(operation, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Confirmed_Operation_Status_Names_Requested_Operation_While_Running()
        {
            var partner = new Mock<IEngineeringPartner>();
            var structured = new Mock<IStructuredConversationPartner>();
            var executor = new Mock<IConfirmedEngineeringOperationExecutor>();
            var completion = new TaskCompletionSource<DevelopmentCommandResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            partner.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            structured.Setup(p => p.SendStructuredReadOnlyMessageAsync(
                    It.IsAny<Guid>(), "Run the tests", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StructuredConversationResult(
                    ConversationResponseKind.ConfirmationRequired,
                    "Confirmation required.",
                    "Run tests",
                    "Run the repository test operation",
                    Operation: EngineeringOperationKind.Test));
            executor.Setup(e => e.ExecuteAsync(EngineeringOperationKind.Test, It.IsAny<CancellationToken>()))
                .Returns(completion.Task);

            var vm = new WorkspaceConversationViewModel(partner.Object, structured.Object, executor.Object);
            await vm.InitializeAsync();
            vm.Draft = "Run the tests";
            await vm.SendCurrentMessageAsync();

            var confirmCommand = Assert.IsType<AsyncRelayCommand>(vm.ConfirmActionCommand);
            confirmCommand.Execute(null);

            Assert.True(vm.IsConfirming);
            Assert.Equal("Running confirmed tests…", vm.StatusText);

            completion.SetResult(new DevelopmentCommandResult(
                DevelopmentCommandKind.Test,
                "dotnet test",
                0,
                TimeSpan.FromSeconds(1),
                Array.Empty<ProblemItem>(),
                "captured output"));
            await confirmCommand.LastTask!;

            Assert.False(vm.IsConfirming);
            Assert.Equal("Ready", vm.StatusText);
        }

        [Fact]
        public async Task Confirmed_Unsupported_State_Change_Is_Not_Executed()
        {
            var partner = new Mock<IEngineeringPartner>();
            var structured = new Mock<IStructuredConversationPartner>();
            var executor = new Mock<IConfirmedEngineeringOperationExecutor>();
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            partner.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            structured.Setup(p => p.SendStructuredReadOnlyMessageAsync(
                    It.IsAny<Guid>(), "Change X", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StructuredConversationResult(
                    ConversationResponseKind.ConfirmationRequired,
                    "Confirmation required.",
                    "State-changing action",
                    "Perform the requested engineering or state-changing action"));

            var vm = new WorkspaceConversationViewModel(partner.Object, structured.Object, executor.Object);
            await vm.InitializeAsync();
            vm.Draft = "Change X";
            await vm.SendCurrentMessageAsync();

            var confirmCommand = Assert.IsType<AsyncRelayCommand>(vm.ConfirmActionCommand);
            confirmCommand.Execute(null);
            await confirmCommand.LastTask!;

            Assert.Contains(vm.Messages, message =>
                message.Text.Contains("Only confirmed Build and Test operations are supported", StringComparison.OrdinalIgnoreCase) &&
                message.Text.Contains("nothing was run or changed", StringComparison.OrdinalIgnoreCase));
            executor.Verify(e => e.ExecuteAsync(It.IsAny<EngineeringOperationKind>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Confirmed_Operation_With_No_Result_Is_Reported_Honestly()
        {
            var partner = new Mock<IEngineeringPartner>();
            var structured = new Mock<IStructuredConversationPartner>();
            var executor = new Mock<IConfirmedEngineeringOperationExecutor>();
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            partner.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            structured.Setup(p => p.SendStructuredReadOnlyMessageAsync(
                    It.IsAny<Guid>(), "Run the tests", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StructuredConversationResult(
                    ConversationResponseKind.ConfirmationRequired,
                    "Confirmation required.",
                    "Run tests",
                    "Run the repository test operation",
                    Operation: EngineeringOperationKind.Test));
            executor.Setup(e => e.ExecuteAsync(EngineeringOperationKind.Test, It.IsAny<CancellationToken>()))
                .ReturnsAsync((DevelopmentCommandResult?)null);

            var vm = new WorkspaceConversationViewModel(partner.Object, structured.Object, executor.Object);
            await vm.InitializeAsync();
            vm.Draft = "Run the tests";
            await vm.SendCurrentMessageAsync();

            var confirmCommand = Assert.IsType<AsyncRelayCommand>(vm.ConfirmActionCommand);
            confirmCommand.Execute(null);
            await confirmCommand.LastTask!;

            Assert.Contains(vm.Messages, message => message.Text.Contains("No result was available", StringComparison.OrdinalIgnoreCase));
            executor.Verify(e => e.ExecuteAsync(EngineeringOperationKind.Test, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Existing_Partner_Falls_Back_To_ReadOnly_String_Path()
        {
            var mock = new Mock<IEngineeringPartner>();
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            mock.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            mock.Setup(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("Read-only reply");

            var vm = new WorkspaceConversationViewModel(mock.Object);
            await vm.InitializeAsync();
            vm.Draft = "What is the current state?";
            await vm.SendCurrentMessageAsync();

            Assert.Contains(vm.Messages, message => message.Text == "Read-only reply");
            mock.Verify(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), "What is the current state?", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task Empty_Message_Is_Ignored()
        {
            var mock = new Mock<IEngineeringPartner>();
            var vm = new WorkspaceConversationViewModel(mock.Object);
            await vm.InitializeAsync();

            vm.Draft = "   ";
            await vm.SendCurrentMessageAsync();

            Assert.DoesNotContain(vm.Messages, m => m.Speaker == "You");
            mock.Verify(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Initialization_Can_Retry_After_A_Service_Failure()
        {
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            var mock = new Mock<IEngineeringPartner>();
            mock.SetupSequence(p => p.StartSessionAsync(It.IsAny<string>()))
                .ThrowsAsync(new InvalidOperationException("unavailable"))
                .ReturnsAsync(model);

            var vm = new WorkspaceConversationViewModel(mock.Object);
            await vm.InitializeAsync();

            Assert.False(vm.IsBusy);
            Assert.Contains("unavailable", vm.StatusText, StringComparison.OrdinalIgnoreCase);

            await vm.InitializeAsync();

            Assert.Equal(model.Id, vm.SessionId);
            Assert.Equal("Ready", vm.StatusText);
            mock.Verify(p => p.StartSessionAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [Fact]
        public async Task Concurrent_Sends_Are_Ignored_While_A_Response_Is_Pending()
        {
            var response = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var model = new EngineeringModel { Id = Guid.NewGuid() };
            var mock = new Mock<IEngineeringPartner>();
            mock.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ReturnsAsync(model);
            mock.Setup(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), It.IsAny<string>())).Returns(response.Task);

            var vm = new WorkspaceConversationViewModel(mock.Object);
            await vm.InitializeAsync();

            vm.Draft = "First question";
            var firstSend = vm.SendCurrentMessageAsync();
            Assert.True(vm.IsBusy);
            Assert.Contains("thinking", vm.StatusText, StringComparison.OrdinalIgnoreCase);

            vm.Draft = "Second question";
            await vm.SendCurrentMessageAsync();

            Assert.Single(vm.Messages.Where(message => message.Speaker == "You"));
            mock.Verify(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), "First question"), Times.Once);
            mock.Verify(p => p.SendReadOnlyMessageAsync(It.IsAny<Guid>(), "Second question"), Times.Never);

            response.SetResult("Contextual answer");
            await firstSend;
            Assert.False(vm.IsBusy);
            Assert.Contains(vm.Messages, message => message.Text == "Contextual answer");
        }

        [Fact]
        public async Task Partner_Failure_Shows_Friendly_Message_And_Resets_Busy_State()
        {
            var mock = new Mock<IEngineeringPartner>();
            mock.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ThrowsAsync(new Exception("no"));

            var vm = new WorkspaceConversationViewModel(mock.Object);
            await vm.InitializeAsync();

            vm.Draft = "Where are we?";
            await vm.SendCurrentMessageAsync();

            Assert.False(vm.IsBusy);
            Assert.Contains(vm.Messages, m => m.Speaker == "EngineOS" && m.Text.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
        }
    }
}
