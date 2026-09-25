# Getting Started with AtlasGT

## Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or VS Code
- Docker Desktop (optional, for infrastructure)

## Installation
1. Clone the repository.
2. Restore NuGet packages: `dotnet restore`.
3. Build the solution: `dotnet build AtlasGT.sln`.
4. Run the API: `dotnet run --project src/AtlasGT.Api/AtlasGT.Api.csproj`.

## Configuration
Modify `appsettings.json` in the API project to configure database connections and default connector settings.
