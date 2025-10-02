FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY src/Fergun.csproj .
RUN dotnet restore

# copy everything else and build app
COPY src/ .
RUN dotnet publish -o out --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Fergun.dll"]