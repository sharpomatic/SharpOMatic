---
title: Assets Helper
sidebar_position: 6
---

Inside a workflow code node, `Assets` is available as a global helper for working with stored assets.
Use it to resolve assets by name, load their content, create new assets, and delete temporary ones.

For the underlying asset model and storage concepts, see [Assets](../core-concepts/assets.md).

## Code Node Access

You do not need to resolve `AssetHelper` from dependency injection inside a code node.
SharpOMatic provides it automatically as the global `Assets`.

The most common pattern is:

- read an `AssetRef` from `Context`
- load bytes, text, or a stream through `Assets`
- create a new asset if you need to persist output
- write the resulting `AssetRef` back into `Context`

```csharp
var inputAsset = Context.Get<AssetRef>("input.document");
var text = await Assets.LoadAssetTextAsync(inputAsset);

var updatedText = text.ToUpperInvariant();
var outputAsset = await Assets.AddAssetFromBytesAsync(
    System.Text.Encoding.UTF8.GetBytes(updatedText),
    "document-uppercase.txt",
    "text/plain");

Context.Set("output.document", outputAsset);
```

## Resolving Assets

### GetAssetRefAsync

Use `GetAssetRefAsync` when you know the asset name and want an `AssetRef` to place into the context or pass to another helper.

```csharp
var logo = await Assets.GetAssetRefAsync("branding/logo.png");
Context.Set("input.logo", logo);
```

Names can be:

- a run asset name
- a library asset name
- a folder-qualified library name such as `branding/logo.png`

When a run is active, the helper checks run assets first and then falls back to library assets.

### GetAssetAsync

Use `GetAssetAsync` when you need metadata such as media type, size, or creation time.

```csharp
var asset = await Assets.GetAssetAsync("templates/system-prompt.md");

Context.Set("output.assetName", asset.Name);
Context.Set("output.mediaType", asset.MediaType);
Context.Set("output.sizeBytes", asset.SizeBytes);
```

## Loading Asset Content

The load methods all support the same input styles:

- `AssetRef`
- `Guid`
- asset name
- `Asset` for some overloads

In code nodes, `AssetRef` is usually the cleanest option because it flows naturally through `Context`.

### LoadAssetBytesAsync

Use `LoadAssetBytesAsync` for binary assets such as images, PDFs, ZIP files, or any content you want to process as raw bytes.

```csharp
var imageAsset = Context.Get<AssetRef>("input.image");
var imageBytes = await Assets.LoadAssetBytesAsync(imageAsset);

Context.Set("output.byteLength", imageBytes.Length);
```

This is the method you will most often use before calling helpers such as `ImageHelper`.

### LoadAssetTextAsync

Use `LoadAssetTextAsync` for prompts, templates, markdown, JSON, CSV, or other text files.

```csharp
var promptAsset = Context.Get<AssetRef>("input.promptTemplate");
var promptText = await Assets.LoadAssetTextAsync(promptAsset);

var userQuestion = Context.Get<string>("input.question");
Context.Set("output.finalPrompt", $"{promptText}\n\nQuestion: {userQuestion}");
```

If you do not specify an encoding, UTF-8 is used.

### LoadAssetStreamAsync

Use `LoadAssetStreamAsync` when you want stream-based processing instead of loading the entire file into memory first.

```csharp
var csvAsset = Context.Get<AssetRef>("input.csvFile");

await using var stream = await Assets.LoadAssetStreamAsync(csvAsset);
using var reader = new StreamReader(stream);

var firstLine = await reader.ReadLineAsync();
Context.Set("output.header", firstLine ?? string.Empty);
```

This is a better fit for large files or APIs that already accept `Stream`.

## Creating Assets

All add methods return an `AssetRef`.
The default scope is `Run`, which is usually what you want for workflow-generated output.

### AddAssetFromBytesAsync

Use `AddAssetFromBytesAsync` when you already have the final content as a `byte[]`.

```csharp
var text = Context.Get<string>("output.finalPrompt");
var bytes = System.Text.Encoding.UTF8.GetBytes(text);

var promptAsset = await Assets.AddAssetFromBytesAsync(
    bytes,
    "final-prompt.txt",
    "text/plain");

Context.Set("output.promptAsset", promptAsset);
```

### AddAssetFromStreamAsync

Use `AddAssetFromStreamAsync` when your content already exists as a stream.

```csharp
var json = """
{
  "success": true,
  "source": "workflow"
}
""";

await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

var jsonAsset = await Assets.AddAssetFromStreamAsync(
    stream,
    "result.json",
    "application/json");

Context.Set("output.resultAsset", jsonAsset);
```

### AddAssetFromBase64Async

Use `AddAssetFromBase64Async` when another node or external API returned base64 text.

```csharp
var base64 = Context.Get<string>("input.base64Image");

var imageAsset = await Assets.AddAssetFromBase64Async(
    base64,
    "generated.png",
    "image/png");

Context.Set("output.generatedImage", imageAsset);
```

### AddAssetFromFileAsync

Use `AddAssetFromFileAsync` when the workflow host can access a local server file.
This reads from the machine running SharpOMatic, not from the browser.

```csharp
var localAsset = await Assets.AddAssetFromFileAsync(
    @"C:\imports\reference.pdf",
    "reference.pdf",
    "application/pdf");

Context.Set("output.referenceFile", localAsset);
```

### AddAssetFromUriAsync

Use `AddAssetFromUriAsync` when you want to download content from a URL and store it as an asset.

```csharp
var uri = new Uri("https://example.com/manual.pdf");

var downloaded = await Assets.AddAssetFromUriAsync(
    uri,
    "manual.pdf",
    "application/pdf");

Context.Set("output.manual", downloaded);
```

### Creating Library Assets

If you want the created asset to be reusable outside the current run, pass `AssetScope.Library`.
To use `AssetScope` in a code node, add its namespace to script options.

```csharp
builder.Services.AddSharpOMaticEngine()
  .AddScriptOptions(
    [typeof(SharpOMatic.Engine.Enumerations.AssetScope).Assembly],
    ["SharpOMatic.Engine.Enumerations"]);
```

Then the code node can create a library asset:

```csharp
var bytes = System.Text.Encoding.UTF8.GetBytes("Shared prompt content");

var sharedAsset = await Assets.AddAssetFromBytesAsync(
    bytes,
    "shared-prompt.txt",
    "text/plain",
    AssetScope.Library);

Context.Set("output.sharedAsset", sharedAsset);
```

## Deleting Assets

Use `DeleteAssetAsync` when you created a temporary asset and no longer want to keep it.
Like the load methods, it accepts multiple input types including `AssetRef`, `Guid`, `Asset`, and name.

```csharp
var tempAsset = Context.Get<AssetRef>("temp.generatedFile");
await Assets.DeleteAssetAsync(tempAsset);

Context.Set("temp.generatedFile", null);
```

Be careful with deletion if you are working with shared library assets.

## Choosing The Right Method

Use:

- `GetAssetRefAsync` when you need a reference by name
- `GetAssetAsync` when you need metadata
- `LoadAssetBytesAsync` for binary content
- `LoadAssetTextAsync` for text content
- `LoadAssetStreamAsync` for large or stream-based processing
- `AddAssetFromBytesAsync` when you already have bytes
- `AddAssetFromStreamAsync` when you already have a stream
- `AddAssetFromBase64Async` when another system returned base64
- `AddAssetFromFileAsync` for local server files
- `AddAssetFromUriAsync` to download and persist remote content
- `DeleteAssetAsync` to clean up assets you no longer need
