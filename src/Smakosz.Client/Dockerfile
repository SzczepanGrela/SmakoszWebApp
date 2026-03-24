FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Smakosz.slnx ./
COPY src/Smakosz.Domain/Smakosz.Domain.csproj src/Smakosz.Domain/
COPY src/Smakosz.Application/Smakosz.Application.csproj src/Smakosz.Application/
COPY src/Smakosz.Client.Shared/Smakosz.Client.Shared.csproj src/Smakosz.Client.Shared/
COPY src/Smakosz.Client.Ops/Smakosz.Client.Ops.csproj src/Smakosz.Client.Ops/
COPY src/Smakosz.Client/Smakosz.Client.csproj src/Smakosz.Client/
RUN dotnet restore src/Smakosz.Client/Smakosz.Client.csproj

COPY src/Smakosz.Domain/ src/Smakosz.Domain/
COPY src/Smakosz.Application/ src/Smakosz.Application/
COPY src/Smakosz.Client.Shared/ src/Smakosz.Client.Shared/
COPY src/Smakosz.Client.Ops/ src/Smakosz.Client.Ops/
COPY src/Smakosz.Client/ src/Smakosz.Client/
RUN test -f src/Smakosz.Client/wwwroot/appsettings.json \
    || echo '{}' > src/Smakosz.Client/wwwroot/appsettings.json
RUN dotnet publish src/Smakosz.Client/Smakosz.Client.csproj -c Release -o /app/publish --no-restore

FROM nginx:alpine AS runtime
RUN apk add --no-cache curl
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
COPY src/Smakosz.Client/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
