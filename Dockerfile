# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["ThinkOnErp.sln", "./"]
COPY ["src/ThinkOnErp.API/ThinkOnErp.API.csproj", "src/ThinkOnErp.API/"]
COPY ["src/ThinkOnErp.Application/ThinkOnErp.Application.csproj", "src/ThinkOnErp.Application/"]
COPY ["src/ThinkOnErp.Domain/ThinkOnErp.Domain.csproj", "src/ThinkOnErp.Domain/"]
COPY ["src/ThinkOnErp.Infrastructure/ThinkOnErp.Infrastructure.csproj", "src/ThinkOnErp.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "src/ThinkOnErp.API/ThinkOnErp.API.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/src/ThinkOnErp.API"
RUN dotnet build "ThinkOnErp.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ThinkOnErp.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user
RUN useradd -m -u 1000 appuser && chown -R appuser:appuser /app
USER appuser

# Copy published application
COPY --from=publish --chown=appuser:appuser /app/publish .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ThinkOnErp.API.dll"]
