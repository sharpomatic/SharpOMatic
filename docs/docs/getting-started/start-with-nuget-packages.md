---
title: Start with NuGet packages
sidebar_position: 2
---

Use these steps if you want to add SharpOMatic to an existing ASP.NET Core project.<br/>
You could start from scratch by creating a new ASP.NET Core project and then running these steps.

## Install packages

```powershell
dotnet add package SharpOMatic.Engine
dotnet add package SharpOMatic.Engine.Sqlite
dotnet add package SharpOMatic.Editor
```

For SQL Server, install `SharpOMatic.Engine.SqlServer` instead of `SharpOMatic.Engine.Sqlite`.

## Register services

Update `Program.cs` to add the required services.
The following example stores library assets and a SQLite database in your user profile.
This gets you up and running quickly and isolates the data to the current user.

For example, if your username is JohnDoe, then the files will be at:<br />
`C:\Users\JohnDoe\AppData\Local\SharpOMatic`

```csharp
  // Assets are stored in the current user's profile
  builder.Services.Configure<FileSystemAssetStoreOptions>(
    builder.Configuration.GetSection("AssetStorage:FileSystem"));
  builder.Services.AddSingleton<IAssetStore, FileSystemAssetStore>();

  builder.Services.AddSharpOMaticEditor();
  builder.Services.AddSharpOMaticTransfer();
  builder.Services.AddSharpOMaticEngine()
    .AddSqliteRepository(
      connectionString: $"Data Source={Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sharpomatic.db")}");
```

The first 3 lines are used to setup how assets are stored.
It uses the file system implementation which is the easiest for getting started.
We want to use the browser based editor and so need to call **AddSharpOMaticEditor**.
To enable import and export we then add **AddSharpOMaticTransfer**.
Finally the **AddSharpOMaticEngine** call is used to setup the repository.
For simplicity we use SQLite, it will create the database automatically on first start.

If you want SQL Server:

```powershell
dotnet add package SharpOMatic.Engine.SqlServer
```

```csharp
  builder.Services.AddSharpOMaticEngine()
    .AddSqlServerRepository(
      connectionString: builder.Configuration.GetConnectionString("SharpOMatic")!);
```

## Map the editor UI

```csharp
  app.MapSharpOMaticEditor("/editor");
```

You only need a single mapping call which specifies the url path for exposing the editor.
If you already use this path for other purposes then you can update this to something more appropriate.

## Open visual editor

Check the generated port number for new project in the `launchSettings.json`.<br/>
NOTE: Replace 9001 with your project specific port number

Use your favorite browser to open http://localhost:9001/editor
