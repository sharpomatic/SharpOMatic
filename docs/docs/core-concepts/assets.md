---
title: Assets
sidebar_position: 3
---

Assets are files that workflows can reference, such as text, images, and other media.
Rather than embedding file contents directly in context (which can bloat the database), store assets separately and reference them.
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
  var asset = await repositoryService.GetLibraryAssetByName("document.png");
  if (asset is null)
      throw new InvalidOperationException("Asset 'document.png' was not found in the library.");

  var assetRef = new AssetRef(asset);

  var context = new ContextObject();
  context.Set("input.image", assetRef);
```

### Add library asset

```csharp
  var data = await File.ReadAllBytesAsync("document.png");

  var libraryAssetRef = await assetService.CreateFromBytesAsync(
      data,
      "document.png",
      "image/png",
      AssetScope.Library);

  var context = new ContextObject();
  context.Set("input.image", libraryAssetRef);
```

### Add run-scoped asset

```csharp
  var data = await File.ReadAllBytesAsync("document.png");

  var runAssetRef = await assetService.CreateFromBytesAsync(
      data,
      "document.png",
      "image/png",
      AssetScope.Run);

  var context = new ContextObject();
  context.Set("input.image", runAssetRef);
```

## Storage

During application configuration, you need to specify how assets will be stored.

### FileSystemAssetStore

This is the simplest implementation and uses your local file system.
It is great for getting up and running quickly, and you can override the default location using appsettings.
It is not recommended for production systems.

### AzureBlobStorageAssetStore

This connects to an Azure-hosted Blob Storage service.
For production systems, it provides a more secure and reliable storage mechanism.
Set appropriate connection settings in your appsettings.
