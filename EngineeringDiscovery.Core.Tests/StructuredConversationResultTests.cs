using System;
using System.Threading.Tasks;
using EngineeringDiscovery.Core.Services;
using EngineeringDiscovery.Core.Tests.Tests;
using Xunit;

namespace EngineeringDiscovery.Core.Tests;

public sealed class StructuredConversationResultTests
{
    [Theory]
    [InlineData("What is the current state?", ConversationResponseKind.Inspection)]
    [InlineData("What changed?", ConversationResponseKind.Inspection)]
    [InlineData("Run the tests.", ConversationResponseKind.ConfirmationRequired)]
    [InlineData("Build it.", ConversationResponseKind.ConfirmationRequired)]
    [InlineData("Change the current direction to testing.", ConversationResponseKind.ConfirmationRequired)]
    [InlineData("What would you do?", ConversationResponseKind.ProposedAction)]
    [InlineData("Launch Kiro and fix it.", ConversationResponseKind.Unsupported)]
    public void Classifier_Distinguishes_Request_Kinds(string request, ConversationResponseKind expected)
    {
        var result = ConversationRequestClassifier.Classify(request);

        Assert.Equal(expected, result.Kind);
        if (expected is ConversationResponseKind.ConfirmationRequired or ConversationResponseKind.ProposedAction)
        {
            Assert.False(string.IsNullOrWhiteSpace(result.ProposedAction));
        }

        if (request.StartsWith("Run the tests", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(EngineeringOperationKind.Test, result.Operation);
        }
        else if (request.StartsWith("Build it", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Equal(EngineeringOperationKind.Build, result.Operation);
        }
    }

    [Fact]
    public async Task Structured_ReadOnly_Request_Records_Confirmation_Without_Executing()
    {
        var partner = new EngineeringPartner(new InMemoryEngineeringModelRepository());
        var session = await partner.StartSessionAsync(string.Empty);

        var result = await partner.SendStructuredReadOnlyMessageAsync(session.Id, "Run the tests");

        Assert.Equal(ConversationResponseKind.ConfirmationRequired, result.Kind);
        Assert.True(result.RequiresConfirmation);
        Assert.Equal("Run the repository test operation", result.ProposedAction);
        Assert.Contains("Confirmation required", result.Reply, StringComparison.Ordinal);
        var model = await partner.GetWorkingMemoryAsync(session.Id);
        Assert.NotNull(model);
        Assert.Equal(result.Reply, model!.Conversation[^1].Message);
        Assert.Contains(model.Conversation, entry => entry.Speaker == "User" && entry.Message == "Run the tests");
    }

    [Fact]
    public async Task Proposal_Request_Never_Requires_Execution_Or_Confirmation()
    {
        var partner = new EngineeringPartner(new InMemoryEngineeringModelRepository());
        var session = await partner.StartSessionAsync(string.Empty);

        var result = await partner.SendStructuredReadOnlyMessageAsync(session.Id, "What would you do?");

        Assert.Equal(ConversationResponseKind.ProposedAction, result.Kind);
        Assert.False(result.RequiresConfirmation);
        Assert.Contains("will not execute", result.Reply, StringComparison.OrdinalIgnoreCase);
    }
}
