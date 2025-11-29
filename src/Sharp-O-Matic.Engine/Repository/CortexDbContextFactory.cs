namespace SharpOMatic.Engine.Repository;

public class SharpOMaticDbContextFactory : IDesignTimeDbContextFactory<SharpOMaticDbContext>
{
    public SharpOMaticDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SharpOMaticDbContext>();

        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbPath = Path.Join(path, "sharpomatic.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        return new SharpOMaticDbContext(optionsBuilder.Options);
    }
}
