FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Smakosz.slnx ./
COPY src/Smakosz.Domain/Smakosz.Domain.csproj src/Smakosz.Domain/
COPY src/Smakosz.Application/Smakosz.Application.csproj src/Smakosz.Application/
COPY src/Smakosz.Infrastructure/Smakosz.Infrastructure.csproj src/Smakosz.Infrastructure/
COPY src/Smakosz.Orchestrator/Smakosz.Orchestrator.csproj src/Smakosz.Orchestrator/
RUN dotnet restore src/Smakosz.Orchestrator/Smakosz.Orchestrator.csproj

COPY src/Smakosz.Domain/ src/Smakosz.Domain/
COPY src/Smakosz.Application/ src/Smakosz.Application/
COPY src/Smakosz.Infrastructure/ src/Smakosz.Infrastructure/
COPY src/Smakosz.Orchestrator/ src/Smakosz.Orchestrator/
RUN dotnet publish src/Smakosz.Orchestrator/Smakosz.Orchestrator.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Smakosz.Orchestrator.dll"]
