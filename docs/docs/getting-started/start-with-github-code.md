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

Check the generated port number for new project in the `launchSettings.json`.<br/>
NOTE: Replace 9001 with your project specific port number

Use your favorite browser to open http://localhost:9001/sharpomatic/editor

## Default SharpOMatic paths

The demo server namespaces the hosted SharpOMatic endpoints under `/sharpomatic` to reduce clashes with existing ASP.NET Core routes:

- Editor UI: `https://localhost:9001/sharpomatic/editor`
- Editor and transfer APIs: `https://localhost:9001/sharpomatic/api/...`
- AG-UI endpoint: `https://localhost:9001/sharpomatic/api/agui`
