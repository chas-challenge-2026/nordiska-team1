using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations;

public partial class AddTaxReportJobNotifications : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION reporting.notify_tax_report_job_pending()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                IF NEW."Status" = 'Pending'
                   AND (TG_OP = 'INSERT' OR OLD."Status" IS DISTINCT FROM NEW."Status") THEN
                    PERFORM pg_notify('tax_report_jobs_pending', NEW."Id"::text);
                END IF;

                RETURN NEW;
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER tax_report_jobs_pending_notification
            AFTER INSERT OR UPDATE OF "Status"
            ON reporting.tax_report_jobs
            FOR EACH ROW
            EXECUTE FUNCTION reporting.notify_tax_report_job_pending();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS tax_report_jobs_pending_notification
            ON reporting.tax_report_jobs;
            """);

        migrationBuilder.Sql("""
            DROP FUNCTION IF EXISTS reporting.notify_tax_report_job_pending();
            """);
    }
}
