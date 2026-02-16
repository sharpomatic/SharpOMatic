namespace SharpOMatic.Engine.Services;

public static class SqliteSharpOMaticBuilderExtensions
{
    public static SharpOMaticBuilder AddSqliteRepository(
        this SharpOMaticBuilder builder,
        string connectionString,
        Action<SharpOMaticDbOptions>? dbOptionsAction = null,
        Action<SqliteDbContextOptionsBuilder>? sqliteOptionsAction = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));

        return builder.AddRepository(
            options =>
            {
                options.UseSqlite(
                    connectionString,
                    sqliteOptions =>
                    {
                        sqliteOptions.MigrationsAssembly(typeof(SqliteSharpOMaticBuilderExtensions).Assembly.FullName);
                        sqliteOptionsAction?.Invoke(sqliteOptions);
                    }
                );
            },
            dbOptionsAction
        );
    }
}
