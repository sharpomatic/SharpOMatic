namespace SharpOMatic.Engine.Repository;

public class SharpOMaticDbContext : DbContext
{
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<Run> Runs { get; set; }
    public DbSet<Trace> Traces { get; set; }

    public SharpOMaticDbContext(DbContextOptions<SharpOMaticDbContext> options)
        : base(options)
    {
    }
}
