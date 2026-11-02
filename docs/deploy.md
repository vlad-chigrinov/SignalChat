# Deploying SignalChat

Two environments on one server, under Docker, behind the Traefik that already runs there (it issues the TLS certificates).
Images are built by GitHub Actions and pulled by Watchtower. Nothing is built on the server.

| | main | stage |
|---|---|---|
| Git branch | `main` | `stage` |
| Image tag | `:main` | `:stage` |
| SPA | `https://chat.vlados.tech` | `https://chat-stage.vlados.tech` |
| API | `https://chat-api.vlados.tech` | `https://chat-api-stage.vlados.tech` |
| ASP.NET Core environment | Production | Development |
| Swagger | no | `https://chat-api-stage.vlados.tech/swagger` |
| Demo data | no | yes (seed at startup) |
| Database | `signalchat_main` | `signalchat_stage` |

PostgreSQL is one shared server: `chat-db.vlados.tech`, login and password, TLS required. Traefik terminates the TLS
(its Let's Encrypt certificate, like the web hosts); the database container itself is not published on the host.

## How a change reaches the server

1. Push to `main` or `stage`.
2. `.github/workflows/images.yml` runs the tests, then builds `signalchat-api` and `signalchat-web` and pushes them to
   `ghcr.io/<owner>/` with the branch name as the tag (and the commit sha as a second tag, for rollbacks). Any failed
   test stops the build. The tests are backend (xUnit), frontend (Vitest, with lint and types) and e2e (Playwright on the
   mock API); all three always run, and the run page shows one "Test report" with a row per kind and the failed tests
   folded under it. The raw results and the Playwright report (with traces) are in the `test-results` artifact, kept for
   7 days.
3. Watchtower on the server (poll every 2 minutes) sees the new `:main` or `:stage` image, pulls it and recreates the
   container. The API applies new database migrations on start.

The SPA image is built for one API address: the workflow passes `https://chat-api.vlados.tech` for `main` and
`https://chat-api-stage.vlados.tech` for `stage` (`API_ORIGIN_MAIN`, `API_ORIGIN_STAGE` at the top of the workflow).
The address goes into the app and into its Content-Security-Policy.

## Files

```
deploy/
  docker-compose.infra.yml    PostgreSQL (shared, TLS through Traefik) and Watchtower
  docker-compose.main.yml     main: api + web
  docker-compose.stage.yml    stage: api (Development, Swagger, seed) + web
  postgres/                   pg_hba.conf, first-start script that creates the two databases and roles
  traefik/postgres-alpn.yml   optional Traefik TLS option for psql 17+ (see "The database")
  env/*.env.example           templates for infra.env, main.env, stage.env (the real files are git-ignored)
```

Copy the whole `deploy/` folder to the server (for example to `/opt/signalchat/`). Compose project names are
`signalchat-infra`, `signalchat-main`, `signalchat-stage`; every container, network and volume is prefixed `signalchat-`,
so other services on the server are not touched.

## One-time setup

### 1. GitHub

- Push the repository, create the `stage` branch from `main`.
- The workflow needs no secrets: it uses the built-in `GITHUB_TOKEN` to push to GHCR. Images are private by default.
- Create a personal access token (classic) with only `read:packages`, for the server.

### 2. Google Cloud Console

Add both redirect URIs to the OAuth client (one client can serve both environments, or make a second one for stage):

- `https://chat-api.vlados.tech/auth/callback/google`
- `https://chat-api-stage.vlados.tech/auth/callback/google`

### 3. DNS

`A` records for `chat`, `chat-api`, `chat-stage`, `chat-api-stage` and `chat-db` (`.vlados.tech`) point at the server.
Traefik issues certificates for all five.

### 4. Traefik: a TCP entrypoint for the database

The web hosts use the existing `websecure` entrypoint. The database needs one more, a TCP entrypoint on the public database
port, in Traefik's static configuration (then restart Traefik once):

```yaml
entryPoints:
  postgres:
    address: ":5432"
```

(or `--entrypoints.postgres.address=:5432`). Open that port in the server's firewall. If Traefik already publishes it,
nothing else is needed; the name `postgres` is the default of `TRAEFIK_DB_ENTRYPOINT`. The database joins Traefik's Docker
network (`TRAEFIK_NETWORK`, default `web`); the compose file declares the router in labels, nothing is added to Traefik's
dynamic configuration. Traefik v3 (tested with 3.6) speaks the PostgreSQL TLS upgrade itself, so the client only has to
use `sslmode=require` or stronger.

### 5. The server

```sh
cd /opt/signalchat/deploy
docker login ghcr.io -u <github-user>          # password: the read:packages token; writes ~/.docker/config.json
cp env/infra.env.example infra.env
cp env/main.env.example  main.env
cp env/stage.env.example stage.env
chmod 600 *.env
```

Fill in the three files (every secret: `openssl rand -base64 36`):

- `infra.env`: `POSTGRES_PASSWORD`, `MAIN_DB_PASSWORD`, `STAGE_DB_PASSWORD`, `REGISTRY_CONFIG_FILE` (absolute path to
  the `config.json` from `docker login`), and the Traefik network, entrypoint and resolver names if they differ from
  the defaults (`web`, `postgres`, `le`).
- `main.env`: `IMAGE_REGISTRY=ghcr.io/<owner>` (lower case), the same `MAIN_DB_PASSWORD`, `PII_ENCRYPTION_KEY`
  (`openssl rand -base64 32`), the Google client id and secret, and the Traefik network, entrypoint and resolver names.
- `stage.env`: the same, with `STAGE_DB_PASSWORD` and **a different** `PII_ENCRYPTION_KEY`.

Back up both `PII_ENCRYPTION_KEY` values in a password manager before the first start. Losing or changing a key makes the
stored emails of that environment unreadable, see [pii-key-rotation.md](pii-key-rotation.md).

### 6. Start

Infrastructure first (the database must exist), then the environments:

```sh
docker compose --env-file infra.env -f docker-compose.infra.yml up -d
docker compose --env-file main.env  -f docker-compose.main.yml  up -d
docker compose --env-file stage.env -f docker-compose.stage.yml up -d
docker compose --env-file main.env  -f docker-compose.main.yml  ps     # healthy
```

From now on Watchtower keeps `main` and `stage` current; the compose files are needed again only when they change.

## Checks after the first start

```sh
curl -sS https://chat-api.vlados.tech/health                      # Healthy
curl -sSI https://chat.vlados.tech/ | grep -iE 'content-security|strict-transport'
curl -sS -o /dev/null -w '%{http_code}\n' https://chat-api.vlados.tech/swagger/index.html        # 404 on main
curl -sS -o /dev/null -w '%{http_code}\n' https://chat-api-stage.vlados.tech/swagger/index.html  # 200 on stage
```

Then sign in with Google on both SPAs. On stage the chat already holds the demo people (Alice, Bob, Carol and Dave Demo),
three rooms with history and an accepted direct chat between Alice and Bob. The demo people cannot sign in; they exist as
chat partners. `main` starts empty.

## The database

Connect with any client (DBeaver, psql) using TLS. The port is the one of Traefik's database entrypoint:

```sh
psql "host=chat-db.vlados.tech port=5432 dbname=signalchat_stage user=signalchat_stage sslmode=require"
```

- Two logins only: `signalchat_main` (database `signalchat_main`) and `signalchat_stage` (database `signalchat_stage`).
  Each reaches its own database and nothing else.
- TLS is Traefik's: a public Let's Encrypt certificate for `chat-db.vlados.tech`, so `sslmode=verify-full` works too. A
  connection without TLS gets no answer (it matches no router). Traefik talks to the database over the internal Docker
  network, so inside PostgreSQL the session is not marked as SSL; that is expected.
- With a wrong password, to another database, or as the superuser the connection is refused (`deploy/postgres/pg_hba.conf`).
  The superuser works only inside the container: `docker exec -it signalchat-db psql -U postgres`.
- The API containers do not go through Traefik: they reach `signalchat-db` on the internal `signalchat-data` network and
  run with `POSTGRES_SSL_MODE=Disable`. Any other container on Traefik's network can also reach the database port, but
  still needs the password.
- **psql / libpq 17 and newer** offer only the TLS ALPN protocol `postgresql`, which Traefik rejects by default
  (`tls: client requested unsupported application protocols`). DBeaver/JDBC, Npgsql and libpq up to 16 are not affected.
  To allow the new clients, copy `deploy/traefik/postgres-alpn.yml` into the directory of Traefik's file provider, set
  `DB_TLS_OPTIONS=postgres-alpn@file` in `infra.env` and run `up -d` on the infra stack. (TLS options cannot be set in
  Docker labels, hence the file. Do not set the name before the file is loaded: the router would stop working.)
- The roles are created once, on the first start of an empty volume. To change a password later:
  `ALTER ROLE signalchat_main PASSWORD '...'`, then update `main.env` and recreate the API container.
- The port is open to the internet: use long random passwords, read the log (`docker logs signalchat-db`, every login is
  logged) and, if you can, restrict the port to known addresses in the server's firewall.

## Watchtower

`nickfedor/watchtower:1.23.2`, pinned (the original `containrrr/watchtower` is archived and fails against current Docker).

- Only containers with the labels `com.centurylinklabs.watchtower.enable=true` and scope `signalchat` are touched: the four
  app containers. The database, Watchtower itself and every other service on the server are left alone.
- It holds the Docker socket, which is root-equivalent access to the host. It is the only container that has it.
- To update Watchtower itself, change the version in `docker-compose.infra.yml` and run `up -d` on the infra stack.

## Roll back

Every build is also tagged with its commit sha. To go back, set that tag in the compose file (`:<sha>` instead of `:main`)
and run `up -d`; remove the Watchtower labels from that container meanwhile or it will move to `:main` again.
Migrations are not undone by switching the image: take a backup first when a release adds one.

```sh
docker exec signalchat-db pg_dump -U postgres signalchat_main | gzip > signalchat-main-$(date +%F).sql.gz
```

## Memory

The host has about 957 MiB shared with other services, so every container has a hard limit and no swap:

| Container | Limit |
|---|---|
| `signalchat-db` | 160 MiB |
| `signalchat-main-api` | 160 MiB (.NET heap capped at 80 MiB) |
| `signalchat-stage-api` | 192 MiB (.NET heap capped at 96 MiB) |
| `signalchat-main-web`, `signalchat-stage-web` | 32 MiB each |
| `signalchat-watchtower` | 64 MiB |

`DOTNET_GCHeapHardLimit` is read as hexadecimal: `5000000` is 80 MiB.

## Known limits

- The stage API runs as ASP.NET Core Development on a public host: Swagger is open and errors may carry details. Stage is
  for testing; keep nothing secret in it. The test sign-in (`E2E_TEST_AUTH`) stays refused there.
- The Data Protection key ring is stored unencrypted in a volume (the API logs a warning). Whoever reads the volume can
  forge session cookies, so keep root access to the server tight.
- The SPA and the API are different origins of one site (`vlados.tech`). The session cookie is `SameSite=Strict` and
  `__Host-`, it is sent to the API from the SPA because both are the same site. This relies on `vlados.tech` not being on
  the public suffix list.
- No `PII_ENCRYPTION_KEY` rotation, no automatic backups: schedule the `pg_dump` above.
- Images are built for `linux/amd64` only.
