using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nordiska.Modules.Reporting.Infrastructure.Db;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations;

[DbContext(typeof(ReportingDbContext))]
[Migration("20260923100000_AddTaxReportJobNotifications")]
public partial class AddTaxReportJobNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION reporting.notify_legacy_tax_report_job_pending()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW."Status" = 'Pending'
                   AND (TG_OP = 'INSERT' OR OLD."Status" IS DISTINCT FROM NEW."Status") THEN
                    PERFORM pg_notify('legacy_tax_report_jobs_pending', NEW."Id"::text);
                END IF;

                RETURN NEW;
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF to_regclass('reporting.legacy_tax_report_jobs') IS NOT NULL THEN
                    EXECUTE '
                        CREATE TRIGGER legacy_tax_report_jobs_pending_notification
                        AFTER INSERT OR UPDATE OF "Status"
                        ON reporting.legacy_tax_report_jobs
                        FOR EACH ROW
                        EXECUTE FUNCTION reporting.notify_legacy_tax_report_job_pending()';
                ELSIF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'reporting'
                      AND table_name = 'tax_report_jobs'
                      AND column_name = 'Year') THEN
                    EXECUTE '
                        CREATE TRIGGER legacy_tax_report_jobs_pending_notification
                        AFTER INSERT OR UPDATE OF "Status"
                        ON reporting.tax_report_jobs
                        FOR EACH ROW
                        EXECUTE FUNCTION reporting.notify_legacy_tax_report_job_pending()';
                END IF;
            END;
            $$;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF to_regclass('reporting.legacy_tax_report_jobs') IS NOT NULL THEN
                    DROP TRIGGER IF EXISTS legacy_tax_report_jobs_pending_notification
                        ON reporting.legacy_tax_report_jobs;
                ELSIF to_regclass('reporting.tax_report_jobs') IS NOT NULL THEN
                    DROP TRIGGER IF EXISTS legacy_tax_report_jobs_pending_notification
                        ON reporting.tax_report_jobs;
                END IF;
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS reporting.notify_legacy_tax_report_job_pending();
            """);
    }
}
