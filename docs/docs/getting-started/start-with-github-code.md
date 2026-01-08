---
title: Start with GitHub code
sidebar_position: 1
---

Use these steps if you want to work directly from source.
This is the fastest way to get up and running and start experimenting with SharpOMatic.
It includes the demo server, editor host, and workflow engine projects.

## Clone the repo

```powershell
git clone https://github.com/sharpomatic/SharpOMatic.git
cd SharpOMatic
```

## Build and run via CLI

```powershell
dotnet build src/SharpOMatic.sln
dotnet run --project src/SharpOMatic.DemoServer
```

## Build and run via Visual Studio

- Use Visual Studio to open `src/SharpOMatic.sln`
- Ensure `SharpOMatic.DemoServer` is the startup project
- Set the configuration to `Release`
- Run

## Open visual editor

Use your favorite browser to open http://localhost:9001/editor
