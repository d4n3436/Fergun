FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore src/Fergun.csproj
# Build and publish a release
RUN dotnet publish src/Fergun.csproj -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Fergun.dll"]