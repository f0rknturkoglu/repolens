# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first with only the project files so dependency layers are cached.
COPY RepoLens.sln ./
COPY src/RepoLens.Domain/RepoLens.Domain.csproj src/RepoLens.Domain/
COPY src/RepoLens.Application/RepoLens.Application.csproj src/RepoLens.Application/
COPY src/RepoLens.Infrastructure/RepoLens.Infrastructure.csproj src/RepoLens.Infrastructure/
COPY src/RepoLens.Api/RepoLens.Api.csproj src/RepoLens.Api/
COPY tests/RepoLens.UnitTests/RepoLens.UnitTests.csproj tests/RepoLens.UnitTests/
COPY tests/RepoLens.IntegrationTests/RepoLens.IntegrationTests.csproj tests/RepoLens.IntegrationTests/
RUN dotnet restore RepoLens.sln

COPY . .
RUN dotnet publish src/RepoLens.Api/RepoLens.Api.csproj --configuration Release --output /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "RepoLens.Api.dll"]
