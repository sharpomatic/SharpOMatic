var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://localhost:9001");
builder.Services.AddCors();
builder.Services.AddControllers().AddJsonOptions(options => 
{
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    options.JsonSerializerOptions.Converters.Add(new NodeEntityConverter());
});
builder.Services.AddSignalR();
builder.Services.AddSingleton<ISharpOMaticEngine, SharpOMaticEngine>();
builder.Services.AddSingleton<ISharpOMaticCode, SharpOMaticCode>();
builder.Services.AddSingleton<ISharpOMaticRepository, SharpOMaticRepository>();
builder.Services.AddSingleton<ISharpOMaticNotification, SharpOMaticNotification>();
builder.Services.AddDbContextFactory<SharpOMaticDbContext>(options =>
{
    var folder = Environment.SpecialFolder.LocalApplicationData;
    var path = Environment.GetFolderPath(folder);
    var dbPath = Path.Join(path, "sharpomatic.db");
    options.UseSqlite($"Data Source={dbPath}");
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<SharpOMaticDbContext>();
        dbContext.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseDefaultFiles();
app.UseStaticFiles();

app.Use(async (context, next) =>
{
    // Buffering ensures we can read the content and then pass it on to the target
    context.Request.EnableBuffering();
    await next();
});

app.MapControllers();
app.MapHub<NotificationHub>("/notifications");
app.Run();

