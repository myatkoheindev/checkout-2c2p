# syntax=docker/dockerfile:1
# Build context: ./backend (see compose.yaml).

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from the project files alone, so a source change does not invalidate the package layer.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Checkout.Domain/Checkout.Domain.csproj src/Checkout.Domain/
COPY src/Checkout.Application/Checkout.Application.csproj src/Checkout.Application/
COPY src/Checkout.Infrastructure/Checkout.Infrastructure.csproj src/Checkout.Infrastructure/
COPY src/Checkout.Api/Checkout.Api.csproj src/Checkout.Api/
RUN dotnet restore src/Checkout.Api/Checkout.Api.csproj

COPY src ./src
RUN dotnet publish src/Checkout.Api/Checkout.Api.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS production
WORKDIR /app
COPY --from=build /out ./

# The base image ships a non-root `app` user; never run the API as root.
USER app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Checkout.Api.dll"]
