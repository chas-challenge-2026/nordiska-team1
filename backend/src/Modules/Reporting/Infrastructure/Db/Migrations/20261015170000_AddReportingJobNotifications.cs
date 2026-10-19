using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nordiska.Modules.Reporting.Infrastructure.Db;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations;

[DbContext(typeof(ReportingDbContext))]
[Migration("20261015170000_AddReportingJobNotifications")]
public partial class AddReportingJobNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION reporting.notify_report_job_pending()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW."Status" = 'Pending'
                   AND (
                       TG_OP = 'INSERT'
                       OR OLD."Status" IS DISTINCT FROM NEW."Status"
                       OR OLD."AvailableAt" IS DISTINCT FROM NEW."AvailableAt"
                   ) THEN
                    PERFORM pg_notify('reporting_jobs_pending', '');
                END IF;

                RETURN NEW;
            END;
            $$;

            REVOKE ALL
                ON FUNCTION reporting.notify_report_job_pending()
                FROM PUBLIC;

            CREATE TRIGGER tax_report_jobs_pending_notification
            AFTER INSERT OR UPDATE OF "Status", "AvailableAt"
            ON reporting.tax_report_jobs
            FOR EACH ROW
            EXECUTE FUNCTION reporting.notify_report_job_pending();

            CREATE TRIGGER account_statement_jobs_pending_notification
            AFTER INSERT OR UPDATE OF "Status", "AvailableAt"
            ON reporting.account_statement_jobs
            FOR EACH ROW
            EXECUTE FUNCTION reporting.notify_report_job_pending();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS tax_report_jobs_pending_notification
                ON reporting.tax_report_jobs;

            DROP TRIGGER IF EXISTS account_statement_jobs_pending_notification
                ON reporting.account_statement_jobs;

            DROP FUNCTION IF EXISTS reporting.notify_report_job_pending();
            """);
    }
}
