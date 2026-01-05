---
title: Start with NuGet packages
sidebar_position: 2
---

Use this path if you want to integrate SharpOMatic into an existing ASP.NET Core host.
Install the engine and editor packages from NuGet.

## Install packages

```powershell
dotnet add package SharpOMatic.Engine
dotnet add package SharpOMatic.Editor
```

## Register services

```csharp
builder.Services.AddSharpOMaticEngine();
builder.Services.AddSharpOMaticEditor();
```

## Map the editor UI

```csharp
app.MapSharpOMaticEditor();
```

If you want to enable transfer import/export, also register `AddSharpOMaticTransfer()`.
