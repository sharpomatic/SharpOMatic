namespace SharpOMatic.Editor;

public static class SharpOMaticEditorExtensions
{
    private const string DefaultBaseHref = "<base href=\"/\">";

    public static WebApplication MapSharpOMaticEditor(this WebApplication app, string path = "/editor")
    {
        ArgumentNullException.ThrowIfNull(app);

        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Editor path must be a non-empty route like '/editor'.", nameof(path));

        var normalizedPath = NormalizePath(path);
        if (string.Equals(normalizedPath, "/", StringComparison.Ordinal))
            throw new ArgumentException("Editor path must be a sub-path like '/editor'.", nameof(path));

        var fileProvider = new ManifestEmbeddedFileProvider(typeof(SharpOMaticEditorExtensions).Assembly, "wwwroot");
        var indexHtml = LoadIndexHtml(fileProvider, normalizedPath);

        app.Map(normalizedPath, editorApp =>
        {
            editorApp.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = fileProvider
            });

            editorApp.Run(async context =>
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.WriteAsync(indexHtml);
            });
        });

        return app;
    }

    private static string LoadIndexHtml(IFileProvider fileProvider, string basePath)
    {
        var fileInfo = fileProvider.GetFileInfo("index.html");
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException(
                "The editor index.html was not found in embedded resources.",
                "index.html");
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var html = reader.ReadToEnd();

        var normalizedBase = basePath.EndsWith("/", StringComparison.Ordinal) ? basePath : basePath + "/";
        if (html.Contains(DefaultBaseHref, StringComparison.OrdinalIgnoreCase))
            return html.Replace(DefaultBaseHref, $"<base href=\"{normalizedBase}\">", StringComparison.OrdinalIgnoreCase);

        var headClose = "</head>";
        var index = html.IndexOf(headClose, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return html;

        var baseTag = $"<base href=\"{normalizedBase}\">{Environment.NewLine}";
        return html.Insert(index, baseTag);
    }


    private static string NormalizePath(string path)
    {
        var trimmed = path.Trim();
        if (!trimmed.StartsWith("/", StringComparison.Ordinal))
            trimmed = "/" + trimmed;

        return trimmed.TrimEnd('/');
    }
}
