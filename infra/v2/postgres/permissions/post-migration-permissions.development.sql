-- LOCAL DEVELOPMENT ONLY.
-- Allows API CRUD across all tables (not migration though)

BEGIN;

REVOKE ALL
    ON ALL TABLES IN SCHEMA banking, faq, reporting
    FROM PUBLIC, nordiska_api;

REVOKE ALL
    ON ALL SEQUENCES IN SCHEMA banking, faq, reporting
    FROM PUBLIC, nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting
    REVOKE ALL ON TABLES FROM nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting
    GRANT SELECT, INSERT, UPDATE, DELETE
    ON TABLES TO nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting
    REVOKE ALL ON SEQUENCES FROM nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting
    GRANT USAGE ON SEQUENCES TO nordiska_api;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON ALL TABLES IN SCHEMA banking, faq, reporting
    TO nordiska_api;

GRANT USAGE
    ON ALL SEQUENCES IN SCHEMA banking, faq, reporting
    TO nordiska_api;

REVOKE ALL
    ON ALL TABLES IN SCHEMA banking, reporting
    FROM nordiska_reporting_worker;

GRANT USAGE
    ON SCHEMA banking, reporting
    TO nordiska_reporting_worker;

GRANT SELECT
    ON ALL TABLES IN SCHEMA banking
    TO nordiska_reporting_worker;

GRANT SELECT, INSERT, UPDATE
    ON ALL TABLES IN SCHEMA reporting
    TO nordiska_reporting_worker;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking
    GRANT SELECT ON TABLES TO nordiska_reporting_worker;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA reporting
    GRANT SELECT, INSERT, UPDATE ON TABLES TO nordiska_reporting_worker;

DO $$
DECLARE
    schema_name text;
BEGIN
    FOREACH schema_name IN ARRAY ARRAY['banking', 'faq', 'reporting']
    LOOP
        IF to_regclass(
            format('%I.%I', schema_name, '__EFMigrationsHistory')
        ) IS NOT NULL THEN
            EXECUTE format(
                'REVOKE ALL ON TABLE %I.%I FROM nordiska_api',
                schema_name,
                '__EFMigrationsHistory'
            );
        END IF;
    END LOOP;
END $$;

COMMIT;