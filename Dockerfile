# ========================
# Step 1: Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy csproj and restore dependencies
COPY ./*.csproj ./
RUN dotnet restore

# Copy the entire project (including Migrations/)
COPY . ./

# Publish
RUN dotnet publish -c Release -o /app/publish

# ========================
# Step 2: Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EgyptOnline.dll"]
