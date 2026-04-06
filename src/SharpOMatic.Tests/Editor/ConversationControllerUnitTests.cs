using Microsoft.AspNetCore.Mvc;
using Moq;
using SharpOMatic.Editor.Controllers;
using SharpOMatic.Editor.DTO;

namespace SharpOMatic.Tests.Editor;

public sealed class ConversationControllerUnitTests
{
    [Fact]
    public async Task StartOrResumeConversation_parses_resume_context_json()
    {
        var workflowId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        NodeResumeInput? capturedResumeInput = null;
        var expectedRun = new Run()
        {
            RunId = Guid.NewGuid(),
            WorkflowId = workflowId,
            ConversationId = conversationId,
            RunStatus = RunStatus.Created,
            Message = "Created",
            Created = DateTime.UtcNow,
            NeedsEditorEvents = true,
            InputContext = "{}",
            OutputContext = "{}",
            Error = string.Empty,
        };

        var engineService = new Mock<IEngineService>();
        engineService
            .Setup(service => service.StartOrResumeConversationAndWait(workflowId, conversationId, It.IsAny<NodeResumeInput?>(), null, true))
            .Callback<Guid, Guid, NodeResumeInput?, ContextEntryListEntity?, bool>((_, _, resumeInput, _, _) => capturedResumeInput = resumeInput)
            .ReturnsAsync(expectedRun);

        var controller = new ConversationController();
        var result = await controller.StartOrResumeConversation(
            engineService.Object,
            workflowId,
            conversationId,
            new ConversationTurnRequest()
            {
                ResumeContextJson = "{\"resume\":{\"answer\":\"final answer\"}}",
                NeedsEditorEvents = true,
            }
        );

        var actionResult = Assert.IsType<ActionResult<Run>>(result);
        Assert.Same(expectedRun, actionResult.Value);
        var mergeResumeInput = Assert.IsType<ContextMergeResumeInput>(capturedResumeInput);
        Assert.Equal("final answer", mergeResumeInput.Context.Get<string>("resume.answer"));
    }

    [Fact]
    public async Task StartOrResumeConversation_rejects_invalid_resume_context_json()
    {
        var controller = new ConversationController();
        var engineService = new Mock<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            controller.StartOrResumeConversation(
                engineService.Object,
                Guid.NewGuid(),
                Guid.NewGuid(),
                new ConversationTurnRequest() { ResumeContextJson = "{invalid" }
            )
        );

        Assert.StartsWith("Resume context json could not be parsed as json.", exception.Message);
    }

    [Fact]
    public async Task StartOrResumeConversation_rejects_non_object_resume_context_json()
    {
        var controller = new ConversationController();
        var engineService = new Mock<IEngineService>();

        var exception = await Assert.ThrowsAsync<SharpOMaticException>(() =>
            controller.StartOrResumeConversation(
                engineService.Object,
                Guid.NewGuid(),
                Guid.NewGuid(),
                new ConversationTurnRequest() { ResumeContextJson = "[1,2,3]" }
            )
        );

        Assert.Equal("Resume context json must be a JSON object.", exception.Message);
    }
}
