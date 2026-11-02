#!/bin/sh
# Runs once, on the first start of an empty data volume (official postgres image: /docker-entrypoint-initdb.d).
# Creates one database and one login role per environment. Each role owns its database and nothing else.
# To change a password later: ALTER ROLE signalchat_main PASSWORD '...'; (this script does not run again).
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres \
  -v main_password="$MAIN_DB_PASSWORD" -v stage_password="$STAGE_DB_PASSWORD" <<'SQL'
CREATE ROLE signalchat_main  LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD :'main_password';
CREATE ROLE signalchat_stage LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD :'stage_password';

CREATE DATABASE signalchat_main  OWNER signalchat_main;
CREATE DATABASE signalchat_stage OWNER signalchat_stage;

-- By default every role may connect to every database. Close that: only the owner gets in.
REVOKE ALL ON DATABASE signalchat_main  FROM PUBLIC;
REVOKE ALL ON DATABASE signalchat_stage FROM PUBLIC;
REVOKE ALL ON DATABASE postgres         FROM PUBLIC;
SQL
