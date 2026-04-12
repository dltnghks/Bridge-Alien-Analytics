FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY BridgeAlien.Analytics.Api/BridgeAlien.Analytics.Api.csproj ./BridgeAlien.Analytics.Api/
RUN dotnet restore ./BridgeAlien.Analytics.Api/BridgeAlien.Analytics.Api.csproj

COPY BridgeAlien.Analytics.Api/ ./BridgeAlien.Analytics.Api/
RUN dotnet publish ./BridgeAlien.Analytics.Api/BridgeAlien.Analytics.Api.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /out .
ENTRYPOINT ["dotnet", "BridgeAlien.Analytics.Api.dll"]
