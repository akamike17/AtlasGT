FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/AtlasGT.Api/AtlasGT.Api.csproj", "src/AtlasGT.Api/"]
COPY ["src/AtlasGT.Application/AtlasGT.Application.csproj", "src/AtlasGT.Application/"]
COPY ["src/AtlasGT.Domain/AtlasGT.Domain.csproj", "src/AtlasGT.Domain/"]
COPY ["src/AtlasGT.Infrastructure/AtlasGT.Infrastructure.csproj", "src/AtlasGT.Infrastructure/"]
RUN dotnet restore "src/AtlasGT.Api/AtlasGT.Api.csproj"
COPY . .
WORKDIR "/src/src/AtlasGT.Api"
RUN dotnet build "AtlasGT.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "AtlasGT.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AtlasGT.Api.dll"]
