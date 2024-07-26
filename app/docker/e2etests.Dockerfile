FROM mcr.microsoft.com/dotnet/sdk:8.0 as base

WORKDIR /src

COPY src/PingApi.E2ETests /src/PingApi.E2ETests
WORKDIR /src/PingApi.E2ETests
RUN dotnet build

CMD [ "dotnet", "test" ]