# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FileProcessor/FileProcessor.csproj", "FileProcessor/"]
RUN dotnet restore "./FileProcessor/FileProcessor.csproj"
COPY . .
WORKDIR "/src/FileProcessor"
RUN dotnet build "./FileProcessor.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FileProcessor.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Ensure the wwwroot directory exists and is accessible
RUN mkdir -p /app/wwwroot

# Install dependencies including ffmpeg
RUN apt-get update && apt-get install -y \ 
	libzen0v5 \
	libmms0 \
	openssl \
	zlib1g \
	zlibc \
	libnghttp2-14 \
	librtmp1 \
	curl \
	libcurl4-gnutls-dev \
	libglib2.0

ENTRYPOINT ["dotnet", "FileProcessor.dll"]