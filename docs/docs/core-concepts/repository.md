---
title: Repository
sidebar_position: 5
---

The repository is the persistent store for workflows and other data.
To provide flexibility in storage options, it uses Entity Framework Core.
Assets are not stored here; they use a separate storage mechanism, while asset metadata is stored in the repository.

## Storage
The major types of data stored are:

- **Workflows**: store workflow metadata plus JSON blobs for nodes and connections.
- **Runs**: store run status, timestamps, and serialized input/output context.
- **Traces**: store per-node execution status, messages, and context snapshots.
- **Evals**: store evaluation definitions and evaluation run results.
- **Metadata**: store predefined metadata for known connectors and models.
- **Connectors**: store user-defined connector instances.
- **Models**: store user-defined model instances.
- **Assets**: store asset metadata and storage keys.
- **Settings**: store runtime configuration such as run history and node limits.

Repository operations are exposed through **IRepositoryService**, which handles upserts, deletions, and queries.

## Configuration

Add one of the provider packages:

```powershell
dotnet add package SharpOMatic.Engine.Sqlite
# or
dotnet add package SharpOMatic.Engine.SqlServer
```

The demo server uses SQLite by default and stores the database file in the users profile data.

```csharp
  builder.Services.AddSharpOMaticEngine()
      .AddSqliteRepository(
          connectionString: $"Data Source={Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sharpomatic.db")}");
```

For SQL Server the connection string is pulled from appsettings.

```csharp
  builder.Services.AddSharpOMaticEngine()
      .AddSqlServerRepository(
          connectionString: builder.Configuration.GetConnectionString("SharpOMatic")!);
```

For local SQL Server testing with LocalDB you can use a connectin string like this.

```text
Server=(localdb)\MSSQLLocalDB;Database=SharpOMatic;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true
```

## Upgrades

Entity Framework migrations are used to update the repository database schema and provide a transparent upgrade path.
By default, migrations are applied automatically on engine startup.
You can disable this by setting **ApplyMigrationsOnStartup** to **false**.
SQLite and SQL Server use separate provider-specific migration chains, each packaged with its provider extension package.
All persisted data types, such as workflows and metadata, have an embedded version number so that version changes can be detected on load and upgrades are applied automatically.
