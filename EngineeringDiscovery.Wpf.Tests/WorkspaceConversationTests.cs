using System;
using System.Threading.Tasks;
using Xunit;
using Moq;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Domain.EngineeringModel;
using EngineeringDiscovery.Wpf.ViewModels;
using System.Windows.Input;

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
            mock.Setup(p => p.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync("Reply from partner");

            var vm = new WorkspaceConversationViewModel(mock.Object);
            // Initialize
            await vm.InitializeAsync("start");

            vm.Draft = "Hello";
            // Execute SendCommand to simulate UI interaction via reflection to avoid WPF ICommand dependency
            var sendCmdProp = vm.GetType().GetProperty("SendCommand");
            Assert.NotNull(sendCmdProp);
            var cmdObj = sendCmdProp.GetValue(vm);
            Assert.NotNull(cmdObj);
            // Execute
            var executeMethod = cmdObj.GetType().GetMethod("Execute");
            Assert.NotNull(executeMethod);
            executeMethod.Invoke(cmdObj, new object[] { null });
            // If the command exposes LastTask, await it
            var lastTaskProp = cmdObj.GetType().GetProperty("LastTask");
            if (lastTaskProp != null)
            {
                var lastTask = lastTaskProp.GetValue(cmdObj) as Task;
                if (lastTask != null) await lastTask;
            }

            Assert.Contains(vm.Messages, m => m.Speaker == "You" && m.Text.Contains("Hello"));
            Assert.Contains(vm.Messages, m => m.Speaker == "Engineering Partner" && m.Text.Contains("Reply from partner"));

            mock.Verify(p => p.StartSessionAsync(It.IsAny<string>()), Times.AtLeastOnce);
            mock.Verify(p => p.SendMessageAsync(It.IsAny<Guid>(), "Hello"), Times.Once);
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
            mock.Verify(p => p.SendMessageAsync(It.IsAny<Guid>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task Partner_Failure_Shows_Friendly_Message()
        {
            var mock = new Mock<IEngineeringPartner>();
            mock.Setup(p => p.StartSessionAsync(It.IsAny<string>())).ThrowsAsync(new Exception("no"));

            var vm = new WorkspaceConversationViewModel(mock.Object);
            await vm.InitializeAsync();

            Assert.Contains(vm.Messages, m => m.Speaker == "Engineering Partner");
        }
    }
}
