using SharpOMatic.Editor;
using SharpOMatic.Server;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:9001");
builder.Services.AddCors();
builder.Services.AddSharpOMaticEditor();
builder.Services.AddSharpOMaticEngine()
    .AddSchemaTypes(typeof(Schema), typeof(StringList))
    .AddToolMethods(ToolCalling.GetGreeting, ToolCalling.GetTime)
    .AddScriptOptions([typeof(ServerHelper).Assembly], ["SharpOMatic.Server"])
    .AddRepository((optionBuilder) =>
    {
        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbPath = Path.Join(path, "sharpomatic.db");
        optionBuilder.UseSqlite($"Data Source={dbPath}");
    }, (dbOptions) =>
    {
        // dbOptions.TablePrefix = "Sample";
        // dbOptions.DefaultSchema = "SharpOMatic";
        // dbOptions.CommandTimeout = 120;
    });

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    // Buffering ensures we can read the content and then pass it on to the target
    context.Request.EnableBuffering();
    await next();
});

app.MapSharpOMaticEditor("/editor");
app.Run();

