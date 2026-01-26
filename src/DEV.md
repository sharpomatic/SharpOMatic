# Development Notes

## Entity Framework Core
C:\Users\philw\AppData\Local

### How to install EF dotnet tools
dotnet tool install --global dotnet-ef

### How to add a new migration
$env:TargetFramework = 'net10.0'
dotnet ef migrations add InitialCreate --framework net10.0
Remove-Item Env:TargetFramework
