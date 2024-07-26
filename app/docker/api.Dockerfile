FROM mcr.microsoft.com/dotnet/sdk:8.0 as base

WORKDIR /src

COPY src/PingApi/PingApi.fsproj /src/PingApi/PingApi.fsproj
WORKDIR /src/PingApi
RUN dotnet restore

COPY src/PingApi.Tests/PingApi.Tests.fsproj /src/PingApi.Tests/PingApi.Tests.fsproj
WORKDIR /src/PingApi.Tests
RUN dotnet restore

COPY src/PingApi /src/PingApi
COPY src/PingApi.Tests /src/PingApi.Tests
WORKDIR /src/PingApi.Tests
RUN dotnet test

RUN mkdir /publish
WORKDIR /src/PingApi
RUN dotnet publish -o /publish -c Release

FROM mcr.microsoft.com/dotnet/aspnet:8.0 as runtime

WORKDIR /app

COPY --from=base /publish .

CMD [ "dotnet", "PingApi.dll" ]