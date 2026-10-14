# ReliefLink API image. Build context = the api folder.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ReliefLink.Api/ReliefLink.Api.csproj ReliefLink.Api/
RUN dotnet restore ReliefLink.Api/ReliefLink.Api.csproj
COPY ReliefLink.Api/ ReliefLink.Api/
RUN dotnet publish ReliefLink.Api/ReliefLink.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
# Do not run as root.
USER app
# Hosting platforms such as Render set PORT; default to 8080.
EXPOSE 8080
CMD ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} exec dotnet ReliefLink.Api.dll"]
