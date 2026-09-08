

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
    GRANT SELECT, INSERT, UPDATE
    ON TABLES TO nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting
    REVOKE ALL ON SEQUENCES FROM nordiska_api;

ALTER DEFAULT PRIVILEGES
    FOR ROLE nordiska_migrator
    IN SCHEMA banking, faq, reporting
    GRANT USAGE ON SEQUENCES TO nordiska_api;

GRANT SELECT, INSERT, UPDATE
    ON ALL TABLES IN SCHEMA banking, faq, reporting
    TO nordiska_api;

GRANT USAGE
    ON ALL SEQUENCES IN SCHEMA banking, faq, reporting
    TO nordiska_api;

GRANT DELETE
    ON TABLE faq.faq_entries
    TO nordiska_api;

REVOKE UPDATE, DELETE, TRUNCATE
    ON TABLE banking.ledger_entries, reporting.audit_entries
    FROM nordiska_api;

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