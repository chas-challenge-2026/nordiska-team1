

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

REVOKE ALL
    ON ALL TABLES IN SCHEMA reporting
    FROM nordiska_reporting_worker;

REVOKE ALL
    ON ALL SEQUENCES IN SCHEMA reporting
    FROM nordiska_reporting_worker;

GRANT SELECT
    ON TABLE reporting.tax_reports,
             reporting.tax_report_jobs,
             reporting.generated_documents,
             reporting.tax_report_documents,
             reporting.account_statements,
             reporting.account_statement_entries,
             reporting.account_statement_jobs,
             reporting.account_statement_documents
    TO nordiska_reporting_worker;

GRANT UPDATE
    ON TABLE reporting.tax_report_jobs,
             reporting.account_statement_jobs
    TO nordiska_reporting_worker;

GRANT INSERT
    ON TABLE reporting.generated_documents,
             reporting.tax_report_documents,
             reporting.account_statement_documents
    TO nordiska_reporting_worker;

GRANT USAGE
    ON SEQUENCE reporting."generated_documents_Id_seq"
    TO nordiska_reporting_worker;

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
