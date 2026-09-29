# 建置內容為專案根目錄（需要一併帶入 apps/auth-shared）：docker build -f apps/developer-api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/auth-shared/AuthShared.csproj apps/auth-shared/
COPY apps/developer-api/DeveloperApi.csproj apps/developer-api/
RUN dotnet restore apps/developer-api/DeveloperApi.csproj
COPY apps/auth-shared apps/auth-shared
COPY apps/developer-api apps/developer-api
RUN dotnet publish apps/developer-api/DeveloperApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --no-create-home --shell /usr/sbin/nologin appuser \
    && mkdir -p /keys/dp \
    && chown -R appuser /keys
COPY --from=build /app .
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "DeveloperApi.dll"]
