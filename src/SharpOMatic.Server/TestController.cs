

using Microsoft.AspNetCore.Http;
using SharpOMatic.Engine.Enumerations;
using SharpOMatic.Engine.Helpers;
using SharpOMatic.Engine.Repository;

namespace SharpOMatic.Server
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestController : ControllerBase
    {
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Test(
            [FromForm] List<IFormFile>? images,
            IEngineService engine,
            IRepositoryService repository,
            IAssetStore assetStore)
        {
            try
            {
                var workflowId = await engine.TryGetWorkflowId("Test");
                var runId = await engine.CreateWorkflowRun(workflowId);

                ContextObject inputContext = [];
                ContextList assetList = [];
                inputContext.Add("image", assetList);

                foreach (var image in images ?? [])
                {
                    if (image is null || image.Length == 0)
                        continue;

                    var assetId = Guid.NewGuid();
                    var storageKey = AssetStorageKey.ForRun(runId, assetId);

                    await using (var stream = image.OpenReadStream())
                        await assetStore.SaveAsync(storageKey, stream, HttpContext.RequestAborted);

                    var mediaType = string.IsNullOrWhiteSpace(image.ContentType) ? "application/octet-stream" : image.ContentType;
                    var name = string.IsNullOrWhiteSpace(image.FileName) ? assetId.ToString("N") : image.FileName;

                    var asset = new Asset
                    {
                        AssetId = assetId,
                        RunId = runId,
                        Name = name,
                        Scope = AssetScope.Run,
                        Created = DateTime.Now,
                        MediaType = mediaType,
                        SizeBytes = image.Length,
                        StorageKey = storageKey
                    };

                    await repository.UpsertAsset(asset);

                    assetList.Add(new AssetRef
                    {
                        AssetId = assetId,
                        Name = asset.Name,
                        MediaType = asset.MediaType,
                        SizeBytes = asset.SizeBytes
                    });
                }

                await engine.StartWorkflowRunAndWait(runId, inputContext);
                var completedRun = await repository.GetRun(runId);
                return Ok(completedRun?.RunStatus.ToString() ?? "Unknown");
            }
            catch (Exception ex)
            {
                return NotFound(ex.Message);
            }
        }
    }
}
