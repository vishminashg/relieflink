# Multi-stage Dockerfile for ReliefLink ASP.NET Core 8 Web API
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first to cache layers
COPY ["ReliefLink.Api/ReliefLink.Api.csproj", "ReliefLink.Api/"]
RUN dotnet restore "ReliefLink.Api/ReliefLink.Api.csproj"

# Copy source and publish
COPY . .
WORKDIR "/src/ReliefLink.Api"
RUN dotnet publish "ReliefLink.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user for container security
USER $APP_UID

COPY --from=build /app/publish .

# Render dynamically sets PORT; ASP.NET Core 8 respects ASPNETCORE_HTTP_PORTS
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ReliefLink.Api.dll"]
