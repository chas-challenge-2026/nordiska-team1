-- DEV ONLY

BEGIN;

REVOKE ALL
    ON SCHEMA banking, faq, reporting, inbox
    FROM PUBLIC, nordiska_api;

GRANT USAGE
    ON SCHEMA banking, faq, reporting, inbox
    TO nordiska_api;

REVOKE ALL
    ON ALL TABLES IN SCHEMA banking, faq, reporting, inbox
    FROM PUBLIC, nordiska_api;

REVOKE ALL
    ON ALL SEQUENCES IN SCHEMA banking, faq, reporting, inbox
    FROM PUBLIC, nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting, inbox
    REVOKE ALL ON TABLES FROM nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting, inbox
    GRANT SELECT, INSERT, UPDATE, DELETE
    ON TABLES TO nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting, inbox
    REVOKE ALL ON SEQUENCES FROM nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting, inbox
    GRANT USAGE ON SEQUENCES TO nordiska_api;

GRANT SELECT, INSERT, UPDATE, DELETE
    ON ALL TABLES IN SCHEMA banking, faq, reporting, inbox
    TO nordiska_api;

GRANT USAGE
    ON ALL SEQUENCES IN SCHEMA banking, faq, reporting, inbox
    TO nordiska_api;

DO $$
DECLARE
    schema_name text;
BEGIN
    FOREACH schema_name IN ARRAY ARRAY['banking', 'faq', 'reporting', 'inbox']
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
