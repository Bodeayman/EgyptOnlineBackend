# ========================
# Step 1: Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy csproj and restore
COPY *.csproj ./
RUN dotnet restore

# Copy the rest and publish
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# ========================
# Step 2: Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish ./

# Expose port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Start the app (migrations run via Program.cs)
ENTRYPOINT ["dotnet", "EgyptOnline.dll"]
