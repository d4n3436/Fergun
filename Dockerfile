FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /app

# copy csproj and restore as distinct layers
COPY src/Fergun.csproj src/Fergun.csproj
RUN dotnet restore src/Fergun.csproj

# copy everything else and build app
COPY . .
RUN dotnet publish src/Fergun.csproj -o out --no-restore

# final stage/image
FROM mcr.microsoft.com/dotnet/runtime:10.0-alpine
WORKDIR /app
COPY --from=build /app/out .
ENTRYPOINT ["dotnet", "Fergun.dll"]