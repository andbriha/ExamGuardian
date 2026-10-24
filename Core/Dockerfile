# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG EnableDevelopmentTools=false

COPY *.sln .
COPY Core/Api.csproj Core/
COPY BusinessLogicLayer/BusinessLogicLayer.csproj BusinessLogicLayer/
COPY DataAccessLayer/DataAccessLayer.csproj DataAccessLayer/

RUN dotnet restore

COPY . .
WORKDIR /src/Core

RUN dotnet build Api.csproj -c Release -o /app/build -p:EnableDevelopmentTools=$EnableDevelopmentTools

# Publish stage
FROM build AS publish
RUN dotnet publish Api.csproj -c Release -o /app/publish /p:UseAppHost=false -p:EnableDevelopmentTools=$EnableDevelopmentTools

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Api.dll"]