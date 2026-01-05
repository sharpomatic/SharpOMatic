---
title: Start with GitHub code
sidebar_position: 1
---

Use this path if you want to work directly from source.
It includes the sample server, editor host, and workflow engine projects.

## Clone and build

```powershell
git clone https://github.com/sharpomatic/SharpOMatic.git
cd SharpOMatic
dotnet build src/SharpOMatic.sln
```

## Run the sample server

```powershell
dotnet run --project src/SharpOMatic.Server
```

The sample server hosts the editor UI and backend APIs so you can test the full workflow.
