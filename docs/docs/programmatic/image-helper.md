---
title: Image Helper
sidebar_position: 5
---

The `ImageHelper` class provides a few utility methods for working with image bytes in workflow code nodes.
It is useful when a workflow or model returns image regions, points, or polygons and you want to either:

- draw those results back onto the original image for debugging or review
- extract sub-images for later processing

The helper lives in the `SharpOMatic.Engine.Helpers` namespace.

## Platform Requirements

`ImageHelper` uses `System.Drawing` and is currently supported on Windows only.
On non-Windows platforms these methods throw `PlatformNotSupportedException`.

Before using these examples inside a code node, make the required namespaces available to the script:

```csharp
builder.Services.AddSharpOMaticEngine()
  .AddScriptOptions(
    [typeof(PointF).Assembly],
    ["System.Drawing"]);
```

Inside a code node, `Assets` is available as a global helper alongside `Context`.
The examples below assume your workflow context contains an `AssetRef` for the source image and any detected shapes.

## Coordinate Format

All coordinates are normalized to the image size.
That means values are typically between `0` and `1`:

- `PointF.X` and `PointF.Y` represent percentages of the image width and height
- `RectangleF.X` and `RectangleF.Y` represent the top-left corner as percentages
- `RectangleF.Width` and `RectangleF.Height` represent the region size as percentages
- polygon points are also specified as normalized `PointF` values

For example, `new RectangleF(0.10f, 0.20f, 0.30f, 0.15f)` means:

- start `10%` from the left
- start `20%` from the top
- use `30%` of the image width
- use `15%` of the image height

Out-of-range values are clamped to the image bounds where possible.
Invalid or empty shapes are skipped.

## Common Example

This example reads an `AssetRef` from the workflow context, loads the image bytes through `Assets`, annotates detected regions and points, and stores the result back as a new run asset.

```csharp
var imageAsset = Context.Get<AssetRef>("input.image");
var imageBytes = await Assets.LoadAssetBytesAsync(imageAsset);

var rectangles = Context.Get<List<RectangleF>>("vision.rectangles");
var titles = Context.Get<List<string>>("vision.labels");
var points = Context.Get<List<PointF>>("vision.points");

var withBoxes = ImageHelper.AnnotateRectangles(imageBytes, rectangles, titles);
var withPoints = ImageHelper.AnnotatePoints(withBoxes, points, radius: 12);

var annotatedAsset = await Assets.AddAssetFromBytesAsync(
    withPoints,
    "receipt-annotated.png",
    "image/png");

Context.Set("output.annotatedImage", annotatedAsset);
```

## Available Helpers

### ExtractRectangles

Use `ExtractRectangles` when you want cropped image regions returned as separate images.
This is useful when a model or OCR step identifies multiple areas and you want to send each area to a later step independently.

```csharp
var imageAsset = Context.Get<AssetRef>("input.image");
var imageBytes = await Assets.LoadAssetBytesAsync(imageAsset);

var crops = ImageHelper.ExtractRectangles(
    imageBytes,
    [
        new RectangleF(0.08f, 0.10f, 0.30f, 0.08f),
        new RectangleF(0.08f, 0.24f, 0.52f, 0.10f),
    ]);

var cropAssets = new ContextList();
for (var i = 0; i < crops.Count; i++)
{
    var cropAsset = await Assets.AddAssetFromBytesAsync(
        crops[i],
        $"crop-{i + 1}.png",
        "image/png");

    cropAssets.Add(cropAsset);
}

Context.Set("output.crops", cropAssets);
```

When to use it:

- split a document into interesting regions before another model call
- save evidence images for auditing
- isolate detected objects or fields for follow-up processing

Notes:

- returns `List<byte[]>`
- each extracted region is returned as PNG bytes
- returns an empty list when no rectangles are supplied

### AnnotatePoints

Use `AnnotatePoints` when you only need to highlight specific coordinates on an image.
This is a good fit for landmarks, click targets, anchor points, or object centers.

```csharp
var imageAsset = Context.Get<AssetRef>("input.image");
var imageBytes = await Assets.LoadAssetBytesAsync(imageAsset);

var annotated = ImageHelper.AnnotatePoints(
    imageBytes,
    [
        new PointF(0.25f, 0.30f),
        new PointF(0.72f, 0.61f),
    ],
    radius: 14);

var annotatedAsset = await Assets.AddAssetFromBytesAsync(
    annotated,
    "points.png",
    "image/png");

Context.Set("output.annotatedImage", annotatedAsset);
```

When to use it:

- mark keypoints returned by a vision model
- visualize where a workflow found important locations
- generate review images for humans

Notes:

- returns PNG bytes for the full image
- if no points are supplied, the original bytes are returned unchanged
- `radius` controls the size of the marker circles

### AnnotateRectangles

Use `AnnotateRectangles` when you want standard bounding-box overlays.
Optional titles are rendered inside colored labels near the top-left of each rectangle.

```csharp
var imageAsset = Context.Get<AssetRef>("input.image");
var imageBytes = await Assets.LoadAssetBytesAsync(imageAsset);

var annotated = ImageHelper.AnnotateRectangles(
    imageBytes,
    [
        new RectangleF(0.10f, 0.12f, 0.24f, 0.18f),
        new RectangleF(0.42f, 0.40f, 0.18f, 0.22f),
    ],
    ["Signature", "Stamp"]);

var annotatedAsset = await Assets.AddAssetFromBytesAsync(
    annotated,
    "boxes.png",
    "image/png");

Context.Set("output.annotatedImage", annotatedAsset);
```

When to use it:

- draw OCR or object-detection bounding boxes
- label regions for debugging model output
- produce annotated images for users or support staff

Notes:

- returns PNG bytes for the full image
- if no rectangles are supplied, the original bytes are returned unchanged
- titles are optional and matched by index

### AnnotatePolygons

Use `AnnotatePolygons` when the result is not well represented by a rectangle.
This is useful for irregular shapes, segmentation-like output, contours, or rotated regions.

```csharp
var imageAsset = Context.Get<AssetRef>("input.image");
var imageBytes = await Assets.LoadAssetBytesAsync(imageAsset);

var polygons = new List<IReadOnlyList<PointF>>
{
    [
        new PointF(0.10f, 0.15f),
        new PointF(0.32f, 0.12f),
        new PointF(0.35f, 0.28f),
        new PointF(0.14f, 0.30f),
    ]
};

var annotated = await ImageHelper.AnnotatePolygons(
    imageBytes,
    polygons,
    ["Detected area"]);

var annotatedAsset = await Assets.AddAssetFromBytesAsync(
    annotated,
    "polygons.png",
    "image/png");

Context.Set("output.annotatedImage", annotatedAsset);
```

When to use it:

- show irregular detected boundaries
- render rotated or perspective-adjusted regions
- visualize segmentation or contour extraction results

Notes:

- returns `Task<byte[]>`
- polygon labels are optional and matched by index
- polygons need at least 3 points

## Choosing The Right Helper

Use:

- `ExtractRectangles` when you need separate cropped images
- `AnnotatePoints` when only point locations matter
- `AnnotateRectangles` for classic bounding boxes
- `AnnotatePolygons` for irregular or non-axis-aligned shapes

In practice, a common pattern is to annotate images for debugging and extract regions for downstream processing.
