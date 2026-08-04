
# ---- Builder ----

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS builder
WORKDIR /src

# Copy solution and project files and restore dependencies (for caching)
COPY TodoDemo.slnx .
COPY TodoDemo/TodoDemo.csproj TodoDemo/
RUN dotnet restore TodoDemo/TodoDemo.csproj

# Copy source code and build the application
COPY TodoDemo/ TodoDemo/
RUN dotnet publish TodoDemo/TodoDemo.csproj \
    --no-restore \
    --output /app/publish \
    --configuration Release \
    -p:UseAppHost=false


# ---- Runtime ----

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health checks and libgssapi-krb5-2 to silence a warning about it being missing
RUN apt-get update && apt-get install -y --no-install-recommends curl libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

# Copy the published application from the builder stage
COPY --from=builder /app/publish .

ENTRYPOINT ["dotnet", "TodoDemo.dll"]