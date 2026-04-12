namespace SharpOMatic.Editor;

public static class SharpOMaticEditorExtensions
{
    private const string DefaultBaseHref = "<base href=\"/\">";
    private const string EditorChildPath = "/editor";
    private const string NotificationsChildPath = "/notifications";

    public static IServiceCollection AddSharpOMaticEditor(this IServiceCollection services, string basePath = SharpOMaticControllerFeatureSetup.DefaultBasePath)
    {
        ArgumentNullException.ThrowIfNull(services);

        var mvcBuilder = services.AddControllers();
        SharpOMaticControllerFeatureSetup.EnsureApplicationPart(mvcBuilder, typeof(SharpOMaticEditorExtensions).Assembly);

        var toggle = SharpOMaticControllerFeatureSetup.GetOrAddToggle(services);
        SharpOMaticControllerFeatureSetup.EnsureBasePath(toggle, basePath);
        SharpOMaticControllerFeatureSetup.EnsureRouteConvention(mvcBuilder, toggle);
        toggle.EnableEditor = true;
        SharpOMaticControllerFeatureSetup.EnsureFeatureProvider(mvcBuilder, toggle);

        mvcBuilder.AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
            options.JsonSerializerOptions.Converters.Add(new NodeEntityConverter());
        });

        services.AddSignalR();
        services.AddSingleton<IProgressService, ProgressService>();

        return services;
    }

    public static WebApplication MapSharpOMaticEditor(this WebApplication app, string? basePath = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var normalizedBasePath = ResolveBasePath(app, basePath);
        var editorPath = SharpOMaticControllerFeatureSetup.CombineBasePath(normalizedBasePath, EditorChildPath);
        var notificationPath = SharpOMaticControllerFeatureSetup.CombineBasePath(normalizedBasePath, NotificationsChildPath);

        var fileProvider = new ManifestEmbeddedFileProvider(typeof(SharpOMaticEditorExtensions).Assembly, "wwwroot");
        var indexHtml = LoadIndexHtml(fileProvider, editorPath);

        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider, RequestPath = editorPath });

        app.MapHub<NotificationHub>(notificationPath);

        MapEditorFallback(app, editorPath, indexHtml);

        return app;
    }

    private static void MapEditorFallback(WebApplication app, string basePath, string indexHtml)
    {
        var fallbackTask = new Func<HttpContext, Task>(context =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            return context.Response.WriteAsync(indexHtml);
        });

        app.MapFallback(basePath, fallbackTask);
        app.MapFallback($"{basePath}/{{*path:nonfile}}", fallbackTask);
    }

    private static string LoadIndexHtml(IFileProvider fileProvider, string basePath)
    {
        var fileInfo = fileProvider.GetFileInfo("index.html");
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("The editor index.html was not found in embedded resources.", "index.html");
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

    private static string ResolveBasePath(WebApplication app, string? basePath)
    {
        var toggle = app.Services.GetService<SharpOMaticControllerToggle>();
        if (string.IsNullOrWhiteSpace(basePath))
            return SharpOMaticControllerFeatureSetup.NormalizeBasePath(toggle?.BasePath);

        var normalizedBasePath = SharpOMaticControllerFeatureSetup.NormalizeBasePath(basePath);
        if (!string.IsNullOrWhiteSpace(toggle?.BasePath) && !string.Equals(toggle.BasePath, normalizedBasePath, StringComparison.Ordinal))
            throw new SharpOMaticException($"MapSharpOMaticEditor base path '{normalizedBasePath}' does not match the registered SharpOMatic controller base path '{toggle.BasePath}'.");

        return normalizedBasePath;
    }
}
