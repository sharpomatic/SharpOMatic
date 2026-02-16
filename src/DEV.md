# Development Notes

## Run Csharpier on Engine
cd c:\code\SharpOMatic\src
dotnet csharpier format SharpOMatic.DemoServer
dotnet csharpier format SharpOMatic.Editor
dotnet csharpier format SharpOMatic.Engine
dotnet csharpier format SharpOMatic.Engine.Sqlite
dotnet csharpier format SharpOMatic.Engine.SqlServer

## Entity Framework Core
C:\Users\philw\AppData\Local

### How to install EF dotnet tools
dotnet tool install --global dotnet-ef

### How to add a new migration
$env:TargetFramework = 'net10.0'

dotnet ef migrations add MigrationName --framework net10.0 --project SharpOMatic.Engine.Sqlite --startup-project SharpOMatic.Engine.Sqlite --context SharpOMaticDbContext --output-dir Migrations

dotnet ef migrations add MigrationName --framework net10.0 --project SharpOMatic.Engine.SqlServer --startup-project SharpOMatic.Engine.SqlServer --context SharpOMaticDbContext --output-dir Migrations

Remove-Item Env:TargetFramework

### LocalDB
Server=(localdb)\MSSQLLocalDB;Database=SharpOMatic;Integrated Security=true;TrustServerCertificate=true;MultipleActiveResultSets=true
