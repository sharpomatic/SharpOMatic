namespace SharpOMatic.Engine.Repository;

public class SharpOMaticDbContext : DbContext
{
    public const string DbSchema = "SharpOMatic";
    public const string MigrationHistoryTableName = "__EFMigrationsHistory";

    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowFolder> WorkflowFolders { get; set; }
    public DbSet<Conversation> Conversations { get; set; }
    public DbSet<ConversationCheckpoint> ConversationCheckpoints { get; set; }
    public DbSet<Run> Runs { get; set; }
    public DbSet<Trace> Traces { get; set; }
    public DbSet<Information> Informations { get; set; }
    public DbSet<StreamEvent> StreamEvents { get; set; }
    public DbSet<ModelCallMetric> ModelCallMetrics { get; set; }
    public DbSet<WorkflowRunMetric> WorkflowRunMetrics { get; set; }
    public DbSet<Asset> Assets { get; set; }
    public DbSet<AssetFolder> AssetFolders { get; set; }
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
        modelBuilder.HasDefaultSchema(DbSchema);

        modelBuilder.Entity<Workflow>().HasOne<WorkflowFolder>().WithMany().HasForeignKey(w => w.WorkflowFolderId).OnDelete(DeleteBehavior.Restrict);

        // Cascade delete: Deleting a Workflow deletes its Runs
        modelBuilder.Entity<Run>().HasOne<Workflow>().WithMany().HasForeignKey(r => r.WorkflowId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting a Workflow deletes its Conversations
        modelBuilder.Entity<Conversation>().HasOne<Workflow>().WithMany().HasForeignKey(c => c.WorkflowId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting a Conversation deletes its Checkpoint
        modelBuilder.Entity<ConversationCheckpoint>().HasOne<Conversation>().WithMany().HasForeignKey(c => c.ConversationId).OnDelete(DeleteBehavior.Cascade);

        // WorkflowId is the cascade owner for Runs; ConversationId is NoAction — callers must delete Runs before deleting a Conversation
        modelBuilder.Entity<Run>().HasOne<Conversation>().WithMany().HasForeignKey(r => r.ConversationId).OnDelete(DeleteBehavior.NoAction);

        // Cascade delete: Deleting a Run deletes its Traces
        modelBuilder.Entity<Trace>().HasOne<Run>().WithMany().HasForeignKey(t => t.RunId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting a Run deletes its Assets
        modelBuilder.Entity<Asset>().HasOne<Run>().WithMany().HasForeignKey(a => a.RunId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting a Run deletes its StreamEvents
        modelBuilder.Entity<StreamEvent>().HasOne<Run>().WithMany().HasForeignKey(s => s.RunId).OnDelete(DeleteBehavior.Cascade);

        // RunId is the cascade owner for Assets; ConversationId is NoAction — callers must delete conversation-scoped assets before deleting a Conversation
        modelBuilder.Entity<Asset>().HasOne<Conversation>().WithMany().HasForeignKey(a => a.ConversationId).OnDelete(DeleteBehavior.NoAction);

        // Cascade delete: Deleting an AssetFolder deletes its Assets
        modelBuilder.Entity<Asset>().HasOne<AssetFolder>().WithMany().HasForeignKey(a => a.FolderId).OnDelete(DeleteBehavior.Restrict);

        // Cascade delete: Deleting an EvalConfig deletes its EvalGraders
        modelBuilder.Entity<EvalGrader>().HasOne<EvalConfig>().WithMany().HasForeignKey(e => e.EvalConfigId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalConfig deletes its EvalColumns
        modelBuilder.Entity<EvalColumn>().HasOne<EvalConfig>().WithMany().HasForeignKey(e => e.EvalConfigId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalConfig deletes its EvalRows
        modelBuilder.Entity<EvalRow>().HasOne<EvalConfig>().WithMany().HasForeignKey(e => e.EvalConfigId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalRow deletes its EvalData
        modelBuilder.Entity<EvalData>().HasOne<EvalRow>().WithMany().HasForeignKey(e => e.EvalRowId).OnDelete(DeleteBehavior.Cascade);

        // EvalRowId is the cascade owner; EvalColumnId is NoAction because DeleteEvalColumn manually ExecuteDeletes EvalData first
        modelBuilder.Entity<EvalData>().HasOne<EvalColumn>().WithMany().HasForeignKey(e => e.EvalColumnId).OnDelete(DeleteBehavior.NoAction);

        // Cascade delete: Deleting an EvalConfig deletes its EvalRuns
        modelBuilder.Entity<EvalRun>().HasOne<EvalConfig>().WithMany().HasForeignKey(e => e.EvalConfigId).OnDelete(DeleteBehavior.Cascade);

        // Cascade delete: Deleting an EvalRun deletes its EvalRunRows
        modelBuilder.Entity<EvalRunRow>().HasOne<EvalRun>().WithMany().HasForeignKey(e => e.EvalRunId).OnDelete(DeleteBehavior.Cascade);

        // EvalRunId is the cascade owner; EvalRowId is NoAction because DeleteEvalRow manually ExecuteDeletes EvalRunRows first
        modelBuilder.Entity<EvalRunRow>().HasOne<EvalRow>().WithMany().HasForeignKey(e => e.EvalRowId).OnDelete(DeleteBehavior.NoAction);

        // Cascade delete: Deleting an EvalRunRow deletes its EvalRunRowGraders
        modelBuilder.Entity<EvalRunRowGrader>().HasOne<EvalRunRow>().WithMany().HasForeignKey(e => e.EvalRunRowId).OnDelete(DeleteBehavior.Cascade);

        // EvalRunRowId is the cascade owner; EvalGraderId is NoAction because DeleteEvalGrader manually ExecuteDeletes EvalRunRowGraders first
        modelBuilder.Entity<EvalRunRowGrader>().HasOne<EvalGrader>().WithMany().HasForeignKey(e => e.EvalGraderId).OnDelete(DeleteBehavior.NoAction);

        // Cascade delete: Deleting an EvalRun deletes its EvalRunGraderSummaries
        modelBuilder.Entity<EvalRunGraderSummary>().HasOne<EvalRun>().WithMany().HasForeignKey(e => e.EvalRunId).OnDelete(DeleteBehavior.Cascade);

        // EvalRunId is the cascade owner; EvalGraderId is NoAction because DeleteEvalGrader manually ExecuteDeletes EvalRunGraderSummaries first
        modelBuilder.Entity<EvalRunGraderSummary>().HasOne<EvalGrader>().WithMany().HasForeignKey(e => e.EvalGraderId).OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ModelCallMetric>().Property(e => e.InputCost).HasPrecision(18, 8);
        modelBuilder.Entity<ModelCallMetric>().Property(e => e.OutputCost).HasPrecision(18, 8);
        modelBuilder.Entity<ModelCallMetric>().Property(e => e.TotalCost).HasPrecision(18, 8);
        modelBuilder.Entity<WorkflowRunMetric>().Property(e => e.TotalModelCost).HasPrecision(18, 8);
    }
}
