# 建置內容為專案根目錄（需要一併帶入 apps/auth-shared）：docker build -f apps/admin-api/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY apps/auth-shared/AuthShared.csproj apps/auth-shared/
COPY apps/admin-api/AdminApi.csproj apps/admin-api/
RUN dotnet restore apps/admin-api/AdminApi.csproj
COPY apps/auth-shared apps/auth-shared
COPY apps/admin-api apps/admin-api
RUN dotnet publish apps/admin-api/AdminApi.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --no-create-home --shell /usr/sbin/nologin appuser
COPY --from=build /app .
USER appuser
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AdminApi.dll"]
