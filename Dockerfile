# ========================
# Step 1: Build stage
# ========================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build

WORKDIR /src

# Copy project file explicitly
COPY EgyptOnline.csproj ./
RUN dotnet restore EgyptOnline.csproj

# Copy everything else
COPY . ./

# Publish
RUN dotnet publish EgyptOnline.csproj -c Release -o /app/publish --no-restore


# ========================
# Step 2: Runtime stage
# ========================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

WORKDIR /app

RUN mkdir -p /app/uploads && chmod 755 /app/uploads

COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "EgyptOnline.dll"]