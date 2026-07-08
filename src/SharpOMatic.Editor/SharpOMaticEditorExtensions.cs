namespace SharpOMatic.Editor;

public static class SharpOMaticEditorExtensions
{
    private const string DefaultBaseHref = "<base href=\"./\">";
    private const string EditorChildPath = "/editor";
    private const string NotificationsChildPath = "/notifications";

    public static IServiceCollection AddSharpOMaticEditor(
        this IServiceCollection services,
        string basePath = SharpOMaticControllerFeatureSetup.DefaultBasePath,
        Action<SharpOMaticEditorOptions>? configureOptions = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var editorOptions = new SharpOMaticEditorOptions();
        configureOptions?.Invoke(editorOptions);
        services.RemoveAll<SharpOMaticEditorOptions>();
        services.AddSingleton(editorOptions);

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
        var options = app.Services.GetService<SharpOMaticEditorOptions>() ?? new SharpOMaticEditorOptions();
        var indexHtml = LoadIndexHtml(fileProvider, editorPath, options);

        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider, RequestPath = editorPath });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = fileProvider, RequestPath = normalizedBasePath });

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

    private static string LoadIndexHtml(IFileProvider fileProvider, string basePath, SharpOMaticEditorOptions options)
    {
        var fileInfo = fileProvider.GetFileInfo("index.html");
        if (!fileInfo.Exists)
        {
            throw new FileNotFoundException("The editor index.html was not found in embedded resources.", "index.html");
        }

        using var stream = fileInfo.CreateReadStream();
        using var reader = new StreamReader(stream, Encoding.UTF8, true);
        var html = reader.ReadToEnd();

        // Inject a runtime script that computes <base href> from window.location.pathname
        // so the correct absolute editor root is used regardless of proxy prefix, trailing
        // slash, or Angular sub-route depth.
        var escapedBasePath = basePath.Replace("'", "\\'");
        var baseScript =
            $"<script>(function(){{var b=document.head.querySelector('base')||document.createElement('base');var p=window.location.pathname;var m='{escapedBasePath}';var i=p.indexOf(m);b.href=(i>=0?p.substring(0,i+m.length):p)+'/';if(!b.parentNode)document.head.insertBefore(b,document.head.firstChild);}})();</script>";
        var headScript = string.IsNullOrWhiteSpace(options.HeadScriptHtml) ? string.Empty : options.HeadScriptHtml + Environment.NewLine;
        var scripts = headScript + baseScript;

        if (html.Contains(DefaultBaseHref, StringComparison.OrdinalIgnoreCase))
            return html.Replace(DefaultBaseHref, scripts, StringComparison.OrdinalIgnoreCase);

        var headClose = "</head>";
        var index = html.IndexOf(headClose, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return html;

        return html.Insert(index, scripts + Environment.NewLine);
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
