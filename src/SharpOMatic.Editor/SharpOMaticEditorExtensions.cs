namespace SharpOMatic.Editor;

public static class SharpOMaticEditorExtensions
{
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
        var indexHtml = LoadIndexHtml(fileProvider);

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

        // Redirect the no-trailing-slash URL to the trailing-slash version using a relative
        // Location so the browser resolves it against its actual (possibly proxy-prefixed) URL.
        var editorChildName = EditorChildPath.TrimStart('/');
        app.MapGet(basePath, context =>
        {
            context.Response.Redirect($"{editorChildName}/", permanent: false);
            return Task.CompletedTask;
        });

        app.MapFallback($"{basePath}/{{*path:nonfile}}", fallbackTask);
    }

    private static string LoadIndexHtml(IFileProvider fileProvider)
    {
        var fileInfo = fileProvider.GetFileInfo("index.html");
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("The editor index.html was not found in embedded resources.", "index.html");
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        return reader.ReadToEnd();
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
