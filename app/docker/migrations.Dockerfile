FROM mcr.microsoft.com/dotnet/sdk:8.0 as base

WORKDIR /app/migrations
COPY src/migrations/.config .config
RUN dotnet tool restore

WORKDIR /app
COPY src/migrations migrations

WORKDIR /app/migrations
CMD ["pwsh", "migrations.ps1"]
