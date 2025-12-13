namespace SharpOMatic.Engine.Repository;

public class SharpOMaticDbContext : DbContext
{
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<Run> Runs { get; set; }
    public DbSet<Trace> Traces { get; set; }
    public DbSet<ConnectorConfigMetadata> ConnectorConfigMetadata { get; set; }
    public DbSet<ConnectorMetadata> ConnectorMetadata { get; set; }
    public DbSet<ModelConfigMetadata> ModelConfigMetadata { get; set; }
    public DbSet<ModelMetadata> ModelMetadata { get; set; }

    private readonly SharpOMaticDbOptions _options;

    public SharpOMaticDbContext(DbContextOptions<SharpOMaticDbContext> options, IOptions<SharpOMaticDbOptions> dbOptions)
        : base(options)
    {
        _options = dbOptions.Value;
        if (_options.CommandTimeout.HasValue)
            Database.SetCommandTimeout(_options.CommandTimeout);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (!string.IsNullOrWhiteSpace(_options.DefaultSchema))
            modelBuilder.HasDefaultSchema(_options.DefaultSchema);

        if (!string.IsNullOrWhiteSpace(_options.TablePrefix))
        {
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
                entity.SetTableName($"{_options.TablePrefix}{entity.GetTableName()}");
        }
    }
}
