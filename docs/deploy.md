# Deploying SignalChat to production

Target: `https://chat.vlados.tech`, Docker, behind the Traefik that already runs on the server.
The images are built on a workstation and loaded on the server. Never build on the server (too little RAM).

## Topology

- One origin, `https://chat.vlados.tech`. Traefik ends TLS and routes by path:
  `/api`, `/auth`, `/hubs`, `/health` go to `signalchat-api`, everything else to `signalchat-web`.
- Three containers from `docker-compose.prod.yml`, all named with the `signalchat-` prefix:
  - `signalchat-api`: ASP.NET Core on `:8080`, non-root, read-only filesystem.
  - `signalchat-web`: nginx with the SPA on `:8080`, non-root, read-only filesystem.
  - `signalchat-db`: PostgreSQL 17 on the internal network only, no published ports.
- Networks: `signalchat-internal` (api and db, no outside access) and the existing Traefik network (api and web).
- Volumes: `signalchat-db-data` (the database) and `signalchat-api-keys` (the Data Protection key ring, which
  keeps session cookies valid across restarts).
- Other services on the server are not touched: no ports are published and every name carries the prefix.

## Before the first deploy

1. Google Cloud Console → Credentials → your OAuth client: add the redirect URI
   `https://chat.vlados.tech/auth/callback/google`.
2. Find out from the server's Traefik config: the network name (`TRAEFIK_NETWORK`), the HTTPS entrypoint
   (usually `websecure`) and the certificate resolver name (`TRAEFIK_CERTRESOLVER`).
3. Create `.env.prod` on the server from `.env.prod.example` (`chmod 600`). Never commit it.
   - `POSTGRES_PASSWORD`: a long random value.
   - `PII_ENCRYPTION_KEY`: `openssl rand -base64 32`. **Back it up in a password manager first.** Losing it or
     changing it locks every user out, see [pii-key-rotation.md](pii-key-rotation.md).
   - `GOOGLE_CLIENT_ID`, `GOOGLE_CLIENT_SECRET`: from step 1.
4. The `chat.vlados.tech` DNS record points at the server.

## Release

On the workstation, from the repository root, on a clean commit of `main` (the tag is the short git sha):

```sh
./scripts/build-images.sh      # or .\scripts\build-images.ps1
./scripts/export-images.sh     # or .\scripts\export-images.ps1
```

`dist-images/` now holds `signalchat-api-<sha>.tar.gz`, `signalchat-web-<sha>.tar.gz` and a `.sha256` file for each.

Copy the two tarballs, their `.sha256` files and `docker-compose.prod.yml` to the server, then there:

```sh
sha256sum -c signalchat-api-<sha>.tar.gz.sha256 signalchat-web-<sha>.tar.gz.sha256
gunzip -c signalchat-api-<sha>.tar.gz | docker load
gunzip -c signalchat-web-<sha>.tar.gz | docker load
```

Set `API_IMAGE=signalchat-api:<sha>` and `WEB_IMAGE=signalchat-web:<sha>` in `.env.prod`, then:

```sh
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d
```

The API applies database migrations on start (`Database__MigrateOnStartup`).

## Check after the start

```sh
docker compose --env-file .env.prod -f docker-compose.prod.yml ps        # all three: healthy
curl -sS https://chat.vlados.tech/health                                  # liveness
curl -sSI https://chat.vlados.tech/ | grep -iE 'content-security|strict-transport|server'
```

Then open `https://chat.vlados.tech`, sign in with Google, send a message from two accounts, create a room.
API responses carry no `Server` header; nginx answers with `Server: nginx` and no version. The `/health` prefix is
routed to the API, so `/health/ready` is public too: it answers only `Healthy` or `Unhealthy`, no details.

## Update

Repeat "Release" with the new sha and change `API_IMAGE` / `WEB_IMAGE`. Take a database backup first, because
migrations run on start and cannot be undone by switching the image back:

```sh
docker exec signalchat-db sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' | gzip > signalchat-$(date +%F).sql.gz
```

## Roll back

Old images stay on the server. Put the previous `API_IMAGE` / `WEB_IMAGE` back in `.env.prod` and run `up -d` again.
If the newer release added a migration, restore the backup from before the update as well.

## Known limits

- No key rotation for `PII_ENCRYPTION_KEY` yet.
- The Data Protection key ring in `signalchat-api-keys` is stored unencrypted (the API logs a warning about it). Whoever
  can read the Docker volume can forge session cookies, so keep root access to the server tight.
- HSTS for the SPA is added by a Traefik middleware in `docker-compose.prod.yml` (one year, no subdomains).
- No automatic backups: schedule the `pg_dump` above on the server.
- DDoS protection and rate limiting at the edge are Traefik's job (middlewares of the existing Traefik, not set
  in this compose). The API has its own per-user rate limits.
