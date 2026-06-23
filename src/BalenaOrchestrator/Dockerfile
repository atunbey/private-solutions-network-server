FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore src/BalenaOrchestrator/BalenaOrchestrator.csproj
RUN dotnet publish src/BalenaOrchestrator/BalenaOrchestrator.csproj -c Release -o /app/out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "BalenaOrchestrator.dll"]
