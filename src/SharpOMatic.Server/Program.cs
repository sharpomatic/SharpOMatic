using SharpOMatic.Engine.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:9001");
builder.Services.AddCors();
builder.Services.AddControllers();
builder.Services.AddSharpOMaticEditor();
builder.Services.Configure<FileSystemAssetStoreOptions>(builder.Configuration.GetSection("AssetStorage:FileSystem"));
builder.Services.AddSingleton<IAssetStore, FileSystemAssetStore>();
builder.Services.AddSharpOMaticEngine()
    .AddSchemaTypes(typeof(TriviaSchema))
    .AddToolMethods(ToolCalling.GetGreeting, ToolCalling.GetTime)
    .AddScriptOptions([typeof(CodeCalling).Assembly], ["SharpOMatic.Server"])
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
        // dbOptions.ApplyMigrationsOnStartup = false;
    });

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapSharpOMaticEditor("/editor");
app.Run();

