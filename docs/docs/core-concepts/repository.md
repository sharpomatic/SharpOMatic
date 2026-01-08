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
- **Metadata**: store predefined metadata for known connectors and models.
- **Connector/Model**: store user-defined connector and model instances.
- **Assets**: store asset metadata and storage keys.
- **Settings**: store runtime configuration such as run history and node limits.

Repository operations are exposed through **IRepositoryService**, which handles upserts, deletions, and queries.

## Configuration

The demo server uses SQLite, but you can point to any supported provider.

```csharp
  builder.Services.AddSharpOMaticEngine()
      .AddRepository(options =>
      {
          var folder = Environment.SpecialFolder.LocalApplicationData;
          var path = Environment.GetFolderPath(folder);
          var dbPath = Path.Join(path, "sharpomatic.db");
          options.UseSqlite($"Data Source={dbPath}");
      });
```

## Upgrades

Entity Framework migrations are used to update the repository database schema and provide a transparent upgrade path.
By default, migrations are applied automatically on engine startup.
You can disable this by setting **ApplyMigrationsOnStartup** to **false**.
All persisted data types, such as workflows and metadata, have an embedded version number so that version changes can be detected on load and upgrades are applied automatically.
