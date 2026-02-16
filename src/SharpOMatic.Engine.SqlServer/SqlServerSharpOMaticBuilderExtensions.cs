namespace SharpOMatic.Engine.Services;

public static class SqlServerSharpOMaticBuilderExtensions
{
    public static SharpOMaticBuilder AddSqlServerRepository(
        this SharpOMaticBuilder builder,
        string connectionString,
        Action<SharpOMaticDbOptions>? dbOptionsAction = null,
        Action<SqlServerDbContextOptionsBuilder>? sqlServerOptionsAction = null
    )
    {
        ArgumentNullException.ThrowIfNull(builder);

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or whitespace.", nameof(connectionString));

        return builder.AddRepository(
            options =>
            {
                options.UseSqlServer(
                    connectionString,
                    sqlServerOptions =>
                    {
                        sqlServerOptions.MigrationsAssembly(typeof(SqlServerSharpOMaticBuilderExtensions).Assembly.FullName);
                        sqlServerOptionsAction?.Invoke(sqlServerOptions);
                    }
                );
            },
            dbOptionsAction
        );
    }
}
