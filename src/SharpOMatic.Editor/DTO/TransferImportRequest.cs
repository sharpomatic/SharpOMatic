namespace SharpOMatic.Editor.DTO;

public class TransferImportRequest
{
    public IFormFile? File { get; set; }
}

public class TransferImportFilesRequest
{
    public List<IFormFile> Files { get; set; } = [];
}
