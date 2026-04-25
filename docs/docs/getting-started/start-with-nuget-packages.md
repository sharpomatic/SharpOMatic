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

If you also want to accept AG-UI clients, install the optional package:

```powershell
dotnet add package SharpOMatic.AGUI
```

## Provider SDK versions

SharpOMatic's model connectors depend on specific OpenAI, Azure OpenAI, Microsoft Agents AI, and Google SDK versions.
When you install the SharpOMatic packages, let NuGet resolve those provider SDK dependencies from the SharpOMatic package graph.

Do not independently upgrade the OpenAI/Azure model-calling packages in the host application unless you are also updating SharpOMatic source code to match the newer SDK APIs.
The OpenAI and Azure OpenAI SDKs are still changing quickly, and newer package combinations can change type names and tool-calling behavior.
SharpOMatic is tested against the provider SDK versions referenced by the current SharpOMatic projects and packages.

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
  builder.Services.AddSharpOMaticAgUi();
  builder.Services.AddSharpOMaticEngine()
    .AddSqliteRepository(
      connectionString: $"Data Source={Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sharpomatic.db")}");
```

The first 3 lines are used to setup how assets are stored.
It uses the file system implementation which is the easiest for getting started.
We want to use the browser based editor and so need to call **AddSharpOMaticEditor**.
To enable import and export we then add **AddSharpOMaticTransfer**.
If you want AG-UI protocol clients, add **AddSharpOMaticAgUi**.
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
  app.MapSharpOMaticEditor();
```

By default SharpOMatic uses a base path of `/sharpomatic`.
That gives you:

- Editor UI: `/sharpomatic/editor`
- Editor and transfer APIs: `/sharpomatic/api/...`
- AG-UI: `/sharpomatic/api/agui`

If you want a different base path, define one variable and use it consistently:

```csharp
  var sharpOMaticBasePath = "/banana";

  builder.Services.AddSharpOMaticEditor(sharpOMaticBasePath);
  builder.Services.AddSharpOMaticTransfer(sharpOMaticBasePath);
  builder.Services.AddSharpOMaticAgUi(sharpOMaticBasePath);

  app.MapSharpOMaticEditor(sharpOMaticBasePath);
```

`MapSharpOMaticEditor` automatically appends `/editor` to the base path.
`AddSharpOMaticAgUi` defaults its child path to `/api/agui`, but you can override that too:

```csharp
  builder.Services.AddSharpOMaticAgUi("/banana", "/integrations/chat");
```

## Open visual editor

Check the generated port number for new project in the `launchSettings.json`.<br/>
NOTE: The demo server uses `https://localhost:9001` and `http://localhost:9000` by default. Replace those ports if your host uses different values.

Use your favorite browser to open https://localhost:9001/sharpomatic/editor
