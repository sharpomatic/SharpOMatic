namespace SharpOMatic.Engine.Repository;

public class SharpOMaticSqlServerDbContextFactory : IDesignTimeDbContextFactory<SharpOMaticDbContext>
{
    public SharpOMaticDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SharpOMaticDbContext>();

        var connectionString = "Server=(localdb)\\mssqllocaldb;Database=SharpOMatic;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        optionsBuilder.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.MigrationsAssembly(typeof(SharpOMaticSqlServerDbContextFactory).Assembly.FullName));

        return new SharpOMaticDbContext(optionsBuilder.Options, Options.Create(new SharpOMaticDbOptions()));
    }
}
