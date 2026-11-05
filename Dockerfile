FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

COPY OnlineShop.sln ./
COPY Entities/Entities.csproj Entities/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Infrastructure.Data/Infrastructure.Data.csproj Infrastructure.Data/
COPY UseCase/UseCase.csproj UseCase/
COPY UseCase.Caching/UseCase.Caching.csproj UseCase.Caching/

RUN dotnet restore OnlineShop.sln

COPY . .
WORKDIR /src/Infrastructure
RUN dotnet publish Infrastructure.csproj -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 3050
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Infrastructure.dll"]
