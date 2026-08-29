FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY LifeManager.sln ./
COPY src/LifeManager.Api/LifeManager.Api.csproj src/LifeManager.Api/
RUN dotnet restore LifeManager.sln
COPY . .
RUN dotnet publish src/LifeManager.Api/LifeManager.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
RUN mkdir -p /app/App_Data
ENTRYPOINT ["dotnet","LifeManager.Api.dll"]
