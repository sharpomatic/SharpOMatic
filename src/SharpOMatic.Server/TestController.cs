
namespace SharpOMatic.Server;

[Route("api/[controller]")]
[ApiController]
public class TestController : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Test(
        [FromForm] List<IFormFile>? images,
        IEngineService engine,
        IAssetService assetService)
    {
        try
        {
            var workflowId = await engine.TryGetWorkflowId("Test");
            var runId = await engine.CreateWorkflowRun(workflowId);

            ContextObject inputContext = [];
            ContextList assetList = [];
            inputContext.Add("image", assetList);

            foreach (var image in images ?? [])
                if ((image is not null) && (image.Length > 0))
                    assetList.Add(await image.CreateAssetRefAsync(assetService, AssetScope.Run, runId, image.Name, image.ContentType));

            var completedRun = await engine.StartWorkflowRunAndWait(runId, inputContext);
            return Ok(completedRun?.RunStatus.ToString() ?? "Unknown");
        }
        catch (Exception ex)
        {
            return NotFound(ex.Message);
        }
    }
}
