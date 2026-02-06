# Development Notes

## Run Csharpier on Engine
cd c:\code\SharpOMatic\src
dotnet csharpier format SharpOMatic.DemoServer
dotnet csharpier format SharpOMatic.Editor
dotnet csharpier format SharpOMatic.Engine

## Entity Framework Core
C:\Users\philw\AppData\Local

### How to install EF dotnet tools
dotnet tool install --global dotnet-ef

### How to add a new migration
$env:TargetFramework = 'net10.0'
dotnet ef migrations add InitialCreate --framework net10.0
Remove-Item Env:TargetFramework
