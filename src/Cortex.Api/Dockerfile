# Multi-stage build for the Cortex API (the bare platform host — no domain modules).
# Build context is the REPOSITORY ROOT:
#   docker build -f src/Cortex.Api/Dockerfile -t cortex-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore using only the project graph + central package management for layer caching.
# .editorconfig is required at build time: it tunes analyzer severities the projects
# rely on under TreatWarningsAsErrors (without it, publish fails on CA1716/CA1711/...).
COPY .editorconfig Directory.Build.props Directory.Build.targets Directory.Packages.props global.json ./
COPY src/Cortex.Core/Cortex.Core.csproj                       src/Cortex.Core/
COPY src/Cortex.Modules.Sdk/Cortex.Modules.Sdk.csproj         src/Cortex.Modules.Sdk/
COPY src/Cortex.Application/Cortex.Application.csproj          src/Cortex.Application/
COPY src/Cortex.Infrastructure/Cortex.Infrastructure.csproj   src/Cortex.Infrastructure/
COPY src/Cortex.AspNetCore/Cortex.AspNetCore.csproj           src/Cortex.AspNetCore/
COPY src/Cortex.ServiceDefaults/Cortex.ServiceDefaults.csproj src/Cortex.ServiceDefaults/
COPY src/Cortex.Api/Cortex.Api.csproj                         src/Cortex.Api/
RUN dotnet restore src/Cortex.Api/Cortex.Api.csproj

# Copy the rest and publish.
COPY src/ src/
RUN dotnet publish src/Cortex.Api/Cortex.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
# Run as the built-in non-root user provided by the aspnet image.
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cortex.Api.dll"]
