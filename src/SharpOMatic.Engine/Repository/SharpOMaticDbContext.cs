namespace SharpOMatic.Engine.Repository;

public class SharpOMaticDbContext : DbContext
{
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<Run> Runs { get; set; }
    public DbSet<Trace> Traces { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<ConnectorConfigMetadata> ConnectorConfigMetadata { get; set; }
    public DbSet<ConnectorMetadata> ConnectorMetadata { get; set; }
    public DbSet<ModelConfigMetadata> ModelConfigMetadata { get; set; }
    public DbSet<ModelMetadata> ModelMetadata { get; set; }
    public DbSet<Setting> Settings { get; set; }

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

        // Cascade delete: Deleting a Workflow deletes its Runs
        modelBuilder.Entity<Run>()
            .HasOne<Workflow>()
            .WithMany()
            .HasForeignKey(r => r.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting a Run deletes its Traces
        modelBuilder.Entity<Trace>()
            .HasOne<Run>()
            .WithMany()
            .HasForeignKey(t => t.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting a Run deletes its Assets
        modelBuilder.Entity<Asset>()
            .HasOne<Run>()
            .WithMany()
            .HasForeignKey(a => a.RunId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
