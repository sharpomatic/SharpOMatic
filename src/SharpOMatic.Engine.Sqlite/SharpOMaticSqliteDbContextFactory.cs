namespace SharpOMatic.Engine.Repository;

public class SharpOMaticSqliteDbContextFactory : IDesignTimeDbContextFactory<SharpOMaticDbContext>
{
    public SharpOMaticDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SharpOMaticDbContext>();

        var folder = Environment.SpecialFolder.LocalApplicationData;
        var path = Environment.GetFolderPath(folder);
        var dbPath = Path.Join(path, "sharpomatic.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}", sqliteOptions => sqliteOptions.MigrationsAssembly(typeof(SharpOMaticSqliteDbContextFactory).Assembly.FullName));

        return new SharpOMaticDbContext(optionsBuilder.Options, Options.Create(new SharpOMaticDbOptions()));
    }
}
