# Etapa 1: restaura, genera el cliente gRPC y publica con el SDK de .NET 10
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS construccion
WORKDIR /src
COPY Portal.csproj .
RUN dotnet restore Portal.csproj
COPY . .
RUN dotnet publish Portal.csproj -c Release -o /app --no-restore

# Etapa 2: imagen de ejecución de ASP.NET Core (Kestrel en el puerto 8080)
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=construccion /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Portal.dll"]
