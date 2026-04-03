namespace SharpOMatic.Editor.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConversationController : ControllerBase
{
    [HttpPost("{workflowId}/{conversationId}")]
    public async Task<ActionResult<Run>> StartOrResumeConversation(
        IEngineService engineService,
        Guid workflowId,
        Guid conversationId,
        [FromBody] ConversationTurnRequest request
    )
    {
        return await engineService.StartOrResumeConversationAndWait(workflowId, conversationId, request.ResumeInput, request.NeedsEditorEvents);
    }

    [HttpGet("{conversationId}")]
    public async Task<ActionResult<Conversation?>> GetConversation(IRepositoryService repositoryService, Guid conversationId)
    {
        return await repositoryService.GetConversation(conversationId);
    }

    [HttpGet("{conversationId}/runs")]
    public async Task<ActionResult<List<Run>>> GetConversationRuns(
        IRepositoryService repositoryService,
        Guid conversationId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 0
    )
    {
        return await repositoryService.GetConversationRuns(conversationId, skip, take);
    }
}
