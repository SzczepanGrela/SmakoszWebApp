FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Smakosz.slnx ./
COPY src/Smakosz.Domain/Smakosz.Domain.csproj src/Smakosz.Domain/
COPY src/Smakosz.Application/Smakosz.Application.csproj src/Smakosz.Application/
COPY src/Smakosz.Infrastructure/Smakosz.Infrastructure.csproj src/Smakosz.Infrastructure/
COPY src/Smakosz.API/Smakosz.API.csproj src/Smakosz.API/
RUN dotnet restore src/Smakosz.API/Smakosz.API.csproj

COPY src/Smakosz.Domain/ src/Smakosz.Domain/
COPY src/Smakosz.Application/ src/Smakosz.Application/
COPY src/Smakosz.Infrastructure/ src/Smakosz.Infrastructure/
COPY src/Smakosz.API/ src/Smakosz.API/
RUN dotnet publish src/Smakosz.API/Smakosz.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "Smakosz.API.dll"]
