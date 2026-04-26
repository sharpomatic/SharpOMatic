---
title: Assets
sidebar_position: 7
---

Assets are files that workflows can reference, such as text, images, and other media.
Rather than embedding file contents directly into context (which can bloat the database), store assets separately and reference them by `AssetRef`.
Depending on their scope, assets can be reused across workflows, reused across conversation turns, or kept private to a single run.

## AssetRef

Instead of passing file contents into context, pass an **AssetRef**.
It contains the unique identifier of the asset along with helpful metadata such as:

- Name
- Media type
- Size

## Asset Scopes

Assets are scoped in three ways:

- **Library** assets are independent of workflows.
- **Run** assets are tied to a specific run.
- **Conversation** assets are tied to a specific conversation across multiple turns.

### Library Assets

Library assets allow for reuse and are not tied to a specific workflow or workflow run.
They are typically added via the editor where you can list, add, move, and delete them.
Library assets can be organized into folders, and those folders are preserved during transfer export/import.
Programmatic changes are also possible.

### Run Assets

Run assets are scoped to a specific workflow run; when that run is deleted, the linked assets are deleted as well.
You cannot view run assets in the editor asset library; instead, navigate to the workflow run.
These assets are typically used when you want to provide a one-off asset for a single run or for images created as part of the run.

### Conversation Assets

Conversation assets are scoped to a specific conversation and remain available across later turns in that same conversation.
When the conversation is deleted, the linked conversation assets are deleted as well.
They do not appear in the editor asset library because they are not shared assets.
Instead, they are shown on the workflow trace panel separately from per-turn run assets.

Conversation assets are useful when a conversation-enabled workflow needs to accumulate artifacts across turns.
For example, you might store a generated transcript, an uploaded reference file, or a draft document that later turns keep reusing.

Only library assets support folders.
Run and conversation assets cannot be assigned to folders.

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

### Add conversation-scoped asset

```csharp
  // File helper loads bytes from a named file on your local system
  var data = await File.ReadAllBytesAsync("transcript.txt");

  // Specify Conversation scope and provide the conversation identifier
  var conversationAssetRef = await assetService.CreateFromBytesAsync(
      data,
      "transcript.txt",
      "text/plain",
      AssetScope.Conversation,
      conversationId: conversationId);

  // Only an AssetRef or AssetRefList can be added into Context
  var context = new ContextObject();
  context.Set("input.transcript", conversationAssetRef);
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
