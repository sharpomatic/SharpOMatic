---
title: Assets
sidebar_position: 6
---

Assets are files that workflows can reference, such as text, images, and other media.
Rather than embedding file contents directly into context (which can bloat the database), store assets separately and reference them.
Assets are also reusable, so the same asset can be used by or passed into multiple workflows.

## AssetRef

Instead of passing file contents into context, pass an **AssetRef**.
It contains the unique identifier of the asset along with helpful metadata such as:

- Name
- Media type
- Size

## Asset Scopes

Assets are scoped in two ways:

- **Library** assets are independent of workflows.
- **Run** assets are tied to a specific run.

### Library Assets

Library assets allow for reuse and are not tied to a specific workflow or workflow run.
They are typically added via the editor where you can list, add, and delete them.
Programmatic changes are also possible, as well as importing from a previous export.

### Run Assets

Run assets are scoped to a specific workflow run; when that run is deleted, the linked assets are deleted as well.
You cannot view run assets in the editor asset library; instead, navigate to the workflow run.
These assets are typically used when you want to provide a one-off asset for a single run or for images created as part of the run.

## Programmatic Examples

You can create and resolve **AssetRef** instances in code, then place them into a context for workflow input.
These examples assume you have resolved **IAssetService** and **IRepositoryService** from dependency injection.

### Use library asset

```csharp
  // Load the named asset
  var asset = await repositoryService.GetLibraryAssetByName("document.png");
  if (asset is null)
      throw new InvalidOperationException("Asset 'document.png' was not found in the library.");

  // Create an AssetRef from the asset
  var assetRef = new AssetRef(asset);

  // Only an AssetRef or AssetRefList can be added into Context
  var context = new ContextObject();
  context.Set("input.image", assetRef);
```

### Add library asset

```csharp
  // File helper loads bytes from a named file on your local system
  var data = await File.ReadAllBytesAsync("document.png");

  // Create new library AssetRef from loaded the bytes
  var libraryAssetRef = await assetService.CreateFromBytesAsync(
      data,
      "document.png",
      "image/png",
      AssetScope.Library);

  // Only an AssetRef or AssetRefList can be added into Context
  var context = new ContextObject();
  context.Set("input.image", libraryAssetRef);
```

### Add run-scoped asset

```csharp
  // File helper loads bytes from a named file on your local system
  var data = await File.ReadAllBytesAsync("document.png");

  // Specify Run scope and provide the run identifier
  var runAssetRef = await assetService.CreateFromBytesAsync(
      data,
      "document.png",
      "image/png",
      AssetScope.Run,
      runId);

  // Only an AssetRef or AssetRefList can be added into Context
  var context = new ContextObject();
  context.Set("input.image", runAssetRef);
```

## Storage

During application configuration, you need to specify how assets will be stored.

### FileSystemAssetStore

This is the simplest implementation and uses your local file system.
It is great for getting up and running quickly, and you can override the default location using appsettings.
It is not recommended for production systems.

To use this store, you need the following in your startup code.

```csharp
  builder.Services.AddSingleton<IAssetStore, FileSystemAssetStore>();
  builder.Services.Configure<FileSystemAssetStoreOptions>(
    builder.Configuration.GetSection("AssetStorage:FileSystem"));
```

By default it will store assets in a subdirectory of your user profile directory.

`C:\Users\<username>\AppData\Local\SharpOMatic\Assets`

To override this, set the **RootPath** in appsettings.

```json
  "AssetStorage": {
    "FileSystem": {
      "RootPath": "C:\\MyStorageDir"
    }
  }
```

### AzureBlobStorageAssetStore

This connects to an Azure-hosted Blob Storage service.
For production systems, it provides a more secure and reliable storage mechanism.
Set appropriate connection settings in your appsettings.

To use this store, you need the following in your startup code.

```csharp
  builder.Services.AddSingleton<IAssetStore, AzureBlobStorageAssetStore>();
  builder.Services.Configure<AzureBlobStorageAssetStoreOptions>(
    builder.Configuration.GetSection("AssetStorage:AzureBlobStorage"));
```

You have two choices for setting the blob storage in appsettings.
Either provide the **ConnectionString** for the blob storage container.

```json
  "AssetStorage": {
    "AzureBlobStorage": {
      "ConnectionString": ""
    }
  }
```

Or you can specify the **ServiceUri** along with the **ContainerName**.

```json
  "AssetStorage": {
    "AzureBlobStorage": {
      "ContainerName": "assets",
      "ServiceUri": ""
    }
  }
```
