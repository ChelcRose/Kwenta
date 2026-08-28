FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Kwenta.csproj ./
RUN dotnet restore Kwenta.csproj

COPY . ./
RUN dotnet publish Kwenta.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish ./

USER $APP_UID
ENTRYPOINT ["dotnet", "Kwenta.dll"]
