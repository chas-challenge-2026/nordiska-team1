#!/bin/sh
set -eu

: "${NORDISKA_MIGRATOR_PASSWORD:?Missing NORDISKA_MIGRATOR_PASSWORD}"
: "${NORDISKA_API_PASSWORD:?Missing NORDISKA_API_PASSWORD}"
: "${NORDISKA_REPORTING_WORKER_PASSWORD:?Missing NORDISKA_REPORTING_WORKER_PASSWORD}"

psql \
  --username "$POSTGRES_USER" \
  --dbname "$POSTGRES_DB" \
  --set ON_ERROR_STOP=on \
  --set database_name="$POSTGRES_DB" \
  --set migrator_password="$NORDISKA_MIGRATOR_PASSWORD" \
  --set api_password="$NORDISKA_API_PASSWORD" \
  --set worker_password="$NORDISKA_REPORTING_WORKER_PASSWORD" <<'SQL'
SET password_encryption = 'scram-sha-256';

REVOKE ALL ON DATABASE :"database_name" FROM PUBLIC;
REVOKE ALL ON SCHEMA public FROM PUBLIC;

CREATE ROLE nordiska_migrator
    LOGIN
    NOSUPERUSER
    NOCREATEDB
    NOCREATEROLE
    NOINHERIT
    NOREPLICATION
    NOBYPASSRLS
    PASSWORD :'migrator_password';

CREATE ROLE nordiska_api
    LOGIN
    NOSUPERUSER
    NOCREATEDB
    NOCREATEROLE
    NOINHERIT
    NOREPLICATION
    NOBYPASSRLS
    PASSWORD :'api_password';

CREATE ROLE nordiska_reporting_worker
    LOGIN
    NOSUPERUSER
    NOCREATEDB
    NOCREATEROLE
    NOINHERIT
    NOREPLICATION
    NOBYPASSRLS
    PASSWORD :'worker_password';

CREATE SCHEMA banking AUTHORIZATION nordiska_migrator;
CREATE SCHEMA faq AUTHORIZATION nordiska_migrator;
CREATE SCHEMA reporting AUTHORIZATION nordiska_migrator;

REVOKE ALL ON SCHEMA banking, faq, reporting FROM PUBLIC;

GRANT CONNECT ON DATABASE :"database_name"
    TO nordiska_migrator, nordiska_api, nordiska_reporting_worker;

GRANT USAGE ON SCHEMA banking, faq, reporting
    TO nordiska_api;

GRANT USAGE ON SCHEMA reporting
    TO nordiska_reporting_worker;

ALTER DEFAULT PRIVILEGES FOR ROLE nordiska_migrator IN SCHEMA banking
    GRANT SELECT, INSERT, UPDATE ON TABLES TO nordiska_api;

ALTER DEFAULT PRIVILEGES FOR ROLE nordiska_migrator IN SCHEMA faq
    GRANT SELECT, INSERT, UPDATE ON TABLES TO nordiska_api;

ALTER DEFAULT PRIVILEGES FOR ROLE nordiska_migrator IN SCHEMA reporting
    GRANT SELECT, INSERT, UPDATE ON TABLES TO nordiska_api;

ALTER DEFAULT PRIVILEGES FOR ROLE nordiska_migrator IN SCHEMA banking
    GRANT USAGE ON SEQUENCES TO nordiska_api;

ALTER DEFAULT PRIVILEGES FOR ROLE nordiska_migrator IN SCHEMA faq
    GRANT USAGE ON SEQUENCES TO nordiska_api;

ALTER DEFAULT PRIVILEGES FOR ROLE nordiska_migrator IN SCHEMA reporting
    GRANT USAGE ON SEQUENCES TO nordiska_api;
SQL

