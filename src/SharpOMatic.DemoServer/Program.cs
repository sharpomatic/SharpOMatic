var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors();
builder.Services.AddControllers();

// --------- SharpOMatic Specific Start ------------------------
//
// Assets are stored in the current users profile
builder.Services.Configure<FileSystemAssetStoreOptions>(
    builder.Configuration.GetSection("AssetStorage:FileSystem")
);
builder.Services.AddSingleton<IAssetStore, FileSystemAssetStore>();

// Provide the controllers and signalr needed by the visual editor
// This and the MapSharpOMaticEditor call go together, use both or neither
builder.Services.AddSharpOMaticEditor();

// Provide the controller needed for data transfer (export/import)
// You might want to be able to import even without the visual editor
builder.Services.AddSharpOMaticTransfer();

// Setup the engine and its capabilties
builder
    .Services.AddSharpOMaticEngine()
    .AddSchemaTypes(typeof(SchemaExample))
    .AddToolMethods(ToolCalling.GetGreeting, ToolCalling.GetTime)
    .AddScriptOptions([typeof(CodeExample).Assembly], ["SharpOMatic.DemoServer"])
    .AddJsonConverters(typeof(ClassExampleConverter))
    .AddRepository(
        (optionBuilder) =>
        {
            // SQLite database in current users profile
            var folder = Environment.SpecialFolder.LocalApplicationData;
            var path = Environment.GetFolderPath(folder);
            var dbPath = Path.Join(path, "sharpomatic.db");
            optionBuilder.UseSqlite($"Data Source={dbPath}");
        }
    );

// Custom implementation to track when workflows are completed
builder.Services.AddSingleton<IProgressService, ProgressService>();

//
// --------- SharpOMatic Specific End --------------------------

var app = builder.Build();

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

// --------- SharpOMatic Specific Start ------------------------
//
// Provide the visual editor at the /editor relative path
app.MapSharpOMaticEditor("/editor");

//
// --------- SharpOMatic Specific End --------------------------

app.Run();
