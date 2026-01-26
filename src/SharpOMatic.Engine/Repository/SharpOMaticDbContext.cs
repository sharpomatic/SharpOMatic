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
    public DbSet<EvalConfig> EvalConfigs { get; set; }
    public DbSet<EvalGrader> EvalGraders { get; set; }
    public DbSet<EvalColumn> EvalColumns { get; set; }
    public DbSet<EvalRow> EvalRows { get; set; }
    public DbSet<EvalData> EvalData { get; set; }
    public DbSet<EvalRun> EvalRuns { get; set; }
    public DbSet<EvalRunRow> EvalRunRows { get; set; }
    public DbSet<EvalRunRowGrader> EvalRunRowGraders { get; set; }
    public DbSet<EvalRunGraderSummary> EvalRunGraderSummaries { get; set; }

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
        modelBuilder.HasDefaultSchema("SharpOMatic");

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

        // Cascade delete: Deleting an EvalConfig deletes its EvalGraders
        modelBuilder.Entity<EvalGrader>()
            .HasOne<EvalConfig>()
            .WithMany()
            .HasForeignKey(e => e.EvalConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalConfig deletes its EvalColumns
        modelBuilder.Entity<EvalColumn>()
            .HasOne<EvalConfig>()
            .WithMany()
            .HasForeignKey(e => e.EvalConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalConfig deletes its EvalRows
        modelBuilder.Entity<EvalRow>()
            .HasOne<EvalConfig>()
            .WithMany()
            .HasForeignKey(e => e.EvalConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalRow deletes its EvalData
        modelBuilder.Entity<EvalData>()
            .HasOne<EvalRow>()
            .WithMany()
            .HasForeignKey(e => e.EvalRowId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalColumn deletes its EvalData
        modelBuilder.Entity<EvalData>()
            .HasOne<EvalColumn>()
            .WithMany()
            .HasForeignKey(e => e.EvalColumnId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalConfig deletes its EvalRuns
        modelBuilder.Entity<EvalRun>()
            .HasOne<EvalConfig>()
            .WithMany()
            .HasForeignKey(e => e.EvalConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalRun deletes its EvalRunRows
        modelBuilder.Entity<EvalRunRow>()
            .HasOne<EvalRun>()
            .WithMany()
            .HasForeignKey(e => e.EvalRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalRow deletes its EvalRunRows
        modelBuilder.Entity<EvalRunRow>()
            .HasOne<EvalRow>()
            .WithMany()
            .HasForeignKey(e => e.EvalRowId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalRunRow deletes its EvalRunRowGraders
        modelBuilder.Entity<EvalRunRowGrader>()
            .HasOne<EvalRunRow>()
            .WithMany()
            .HasForeignKey(e => e.EvalRunRowId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalGrader deletes its EvalRunRowGraders
        modelBuilder.Entity<EvalRunRowGrader>()
            .HasOne<EvalGrader>()
            .WithMany()
            .HasForeignKey(e => e.EvalGraderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalRun deletes its EvalRunGraderSummaries
        modelBuilder.Entity<EvalRunGraderSummary>()
            .HasOne<EvalRun>()
            .WithMany()
            .HasForeignKey(e => e.EvalRunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalGrader deletes its EvalRunGraderSummaries
        modelBuilder.Entity<EvalRunGraderSummary>()
            .HasOne<EvalGrader>()
            .WithMany()
            .HasForeignKey(e => e.EvalGraderId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
