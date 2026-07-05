# Multi-stage build for the Cortex SAMPLE host — the full runnable product: platform +
# Finance/Nutrition/Legal modules + connectors + the brand-voice sample skill.
# Build context is the REPOSITORY ROOT:
#   docker build -f samples/Cortex.Sample.Host/Dockerfile -t cortex-sample .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore using only the project graph + central package management for layer caching.
# .editorconfig is required at build time: it tunes analyzer severities the projects
# rely on under TreatWarningsAsErrors (without it, publish fails on CA1716/CA1711/...).
COPY .editorconfig Directory.Build.props Directory.Build.targets Directory.Packages.props global.json ./
COPY src/Cortex.Core/Cortex.Core.csproj                             src/Cortex.Core/
COPY src/Cortex.Modules.Sdk/Cortex.Modules.Sdk.csproj               src/Cortex.Modules.Sdk/
COPY src/Cortex.Application/Cortex.Application.csproj               src/Cortex.Application/
COPY src/Cortex.Infrastructure/Cortex.Infrastructure.csproj         src/Cortex.Infrastructure/
COPY src/Cortex.AspNetCore/Cortex.AspNetCore.csproj                 src/Cortex.AspNetCore/
COPY src/Cortex.ServiceDefaults/Cortex.ServiceDefaults.csproj       src/Cortex.ServiceDefaults/
COPY src/Cortex.Connectors.Sdk/Cortex.Connectors.Sdk.csproj         src/Cortex.Connectors.Sdk/
COPY src/Cortex.Connectors/Cortex.Connectors.csproj                 src/Cortex.Connectors/
COPY samples/Cortex.Modules.Finance/Cortex.Modules.Finance.csproj     samples/Cortex.Modules.Finance/
COPY samples/Cortex.Modules.Nutrition/Cortex.Modules.Nutrition.csproj samples/Cortex.Modules.Nutrition/
COPY samples/Cortex.Modules.Legal/Cortex.Modules.Legal.csproj         samples/Cortex.Modules.Legal/
COPY samples/Cortex.Sample.Host/Cortex.Sample.Host.csproj             samples/Cortex.Sample.Host/
RUN dotnet restore samples/Cortex.Sample.Host/Cortex.Sample.Host.csproj

# Copy the rest and publish (the skills/ directory rides along as Content).
COPY src/ src/
COPY samples/Cortex.Modules.Finance/   samples/Cortex.Modules.Finance/
COPY samples/Cortex.Modules.Nutrition/ samples/Cortex.Modules.Nutrition/
COPY samples/Cortex.Modules.Legal/     samples/Cortex.Modules.Legal/
COPY samples/Cortex.Sample.Host/       samples/Cortex.Sample.Host/
RUN dotnet publish samples/Cortex.Sample.Host/Cortex.Sample.Host.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
# Run as the built-in non-root user provided by the aspnet image.
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Cortex.Sample.Host.dll"]
