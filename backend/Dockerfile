# Built on the workstation, never on the production host. Build context is the backend/ directory.
# Alpine images: they ship BusyBox wget, so the container healthcheck needs no curl.

FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src
COPY global.json ./
COPY src ./src
RUN dotnet publish src/SignalChat.Api/SignalChat.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# Production by default: the host then requires DATA_PROTECTION_KEYS_PATH and refuses E2E_TEST_AUTH.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    DATA_PROTECTION_KEYS_PATH=/var/lib/signalchat/keys

COPY --from=build /app/publish ./

# The key ring directory is owned by the non-root user; compose mounts a volume over it.
RUN mkdir -p /var/lib/signalchat/keys && chown app:app /var/lib/signalchat/keys

# The aspnet image ships the non-root user "app" (uid 1654).
USER app
EXPOSE 8080

ENTRYPOINT ["dotnet", "SignalChat.Api.dll"]
