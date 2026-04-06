# ========================
# Step 1: Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy csproj and restore dependencies (allows layer caching)
COPY ./*.csproj ./
RUN dotnet restore

# Copy everything else
COPY . ./

# Publish to /src/out
RUN dotnet publish -c Release -o /src/out


# ========================
# Step 2: Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

# Create uploads directory with proper permissions
RUN mkdir -p /app/uploads && chmod 755 /app/uploads

# Copy output from build stage
COPY --from=build /src/out ./

# Use 8080 to match your Dockerfile
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EgyptOnline.dll"]