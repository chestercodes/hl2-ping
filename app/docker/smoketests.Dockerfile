FROM mcr.microsoft.com/dotnet/sdk:8.0 as base

WORKDIR /src

COPY src/PingApi.SmokeTests /src/PingApi.SmokeTests
WORKDIR /src/PingApi.SmokeTests
RUN dotnet build

CMD [ "dotnet", "test" ]