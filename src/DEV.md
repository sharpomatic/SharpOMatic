# Development Notes

## Entity Framework Core

### How to install EF dotnet tools4
dotnet tool install --global dotnet-ef

### How to add a new migration
$env:TargetFramework = 'net10.0'
dotnet ef migrations add InitialCreate --framework net10.0
Remove-Item Env:TargetFramework
