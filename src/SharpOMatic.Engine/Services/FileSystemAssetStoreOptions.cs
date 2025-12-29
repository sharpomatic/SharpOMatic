namespace SharpOMatic.Engine.Services;

public sealed class FileSystemAssetStoreOptions
{
    public static string DefaultRootPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SharpOMatic", "Assets");

    public string RootPath { get; set; } = DefaultRootPath;
}
