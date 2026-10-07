using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Nordiska.Modules.Reporting.Infrastructure.Db;

#nullable disable

namespace Nordiska.Modules.Reporting.Infrastructure.Db.Migrations;

[DbContext(typeof(ReportingDbContext))]
[Migration("20261006043640_ArchiveLegacyReportingTables")]
public partial class ArchiveLegacyReportingTables : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'reporting'
                      AND table_name = 'tax_reports'
                      AND column_name = 'Year')
                   AND to_regclass('reporting.legacy_tax_reports') IS NULL THEN
                    ALTER TABLE reporting.tax_reports
                        RENAME TO legacy_tax_reports;
                    ALTER TABLE reporting.legacy_tax_reports
                        RENAME CONSTRAINT "PK_tax_reports" TO "PK_legacy_tax_reports";
                    ALTER INDEX reporting."IX_tax_reports_AccountId_Year"
                        RENAME TO "IX_legacy_tax_reports_AccountId_Year";
                END IF;

                IF EXISTS (
                    SELECT 1
                    FROM information_schema.columns
                    WHERE table_schema = 'reporting'
                      AND table_name = 'tax_report_jobs'
                      AND column_name = 'Year')
                   AND to_regclass('reporting.legacy_tax_report_jobs') IS NULL THEN
                    IF EXISTS (
                        SELECT 1
                        FROM pg_trigger
                        WHERE tgname = 'tax_report_jobs_pending_notification'
                          AND tgrelid = 'reporting.tax_report_jobs'::regclass) THEN
                        ALTER TRIGGER tax_report_jobs_pending_notification
                            ON reporting.tax_report_jobs
                            RENAME TO legacy_tax_report_jobs_pending_notification;
                    END IF;

                    ALTER TABLE reporting.tax_report_jobs
                        RENAME TO legacy_tax_report_jobs;
                    ALTER TABLE reporting.legacy_tax_report_jobs
                        RENAME CONSTRAINT "PK_tax_report_jobs" TO "PK_legacy_tax_report_jobs";
                    ALTER INDEX reporting."IX_tax_report_jobs_AccountId_Year"
                        RENAME TO "IX_legacy_tax_report_jobs_AccountId_Year";
                    ALTER INDEX reporting."IX_tax_report_jobs_CustomerId_CreatedAt"
                        RENAME TO "IX_legacy_tax_report_jobs_CustomerId_CreatedAt";
                    ALTER INDEX reporting."IX_tax_report_jobs_Status_CreatedAt"
                        RENAME TO "IX_legacy_tax_report_jobs_Status_CreatedAt";
                END IF;

                IF to_regprocedure('reporting.notify_tax_report_job_pending()') IS NOT NULL
                   AND to_regprocedure('reporting.notify_legacy_tax_report_job_pending()') IS NULL THEN
                    ALTER FUNCTION reporting.notify_tax_report_job_pending()
                        RENAME TO notify_legacy_tax_report_job_pending;
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
                IF to_regclass('reporting.legacy_tax_report_jobs') IS NOT NULL
                   AND to_regclass('reporting.tax_report_jobs') IS NULL THEN
                    ALTER TABLE reporting.legacy_tax_report_jobs
                        RENAME CONSTRAINT "PK_legacy_tax_report_jobs" TO "PK_tax_report_jobs";
                    ALTER INDEX reporting."IX_legacy_tax_report_jobs_AccountId_Year"
                        RENAME TO "IX_tax_report_jobs_AccountId_Year";
                    ALTER INDEX reporting."IX_legacy_tax_report_jobs_CustomerId_CreatedAt"
                        RENAME TO "IX_tax_report_jobs_CustomerId_CreatedAt";
                    ALTER INDEX reporting."IX_legacy_tax_report_jobs_Status_CreatedAt"
                        RENAME TO "IX_tax_report_jobs_Status_CreatedAt";
                    ALTER TABLE reporting.legacy_tax_report_jobs
                        RENAME TO tax_report_jobs;

                    IF EXISTS (
                        SELECT 1
                        FROM pg_trigger
                        WHERE tgname = 'legacy_tax_report_jobs_pending_notification'
                          AND tgrelid = 'reporting.tax_report_jobs'::regclass) THEN
                        ALTER TRIGGER legacy_tax_report_jobs_pending_notification
                            ON reporting.tax_report_jobs
                            RENAME TO tax_report_jobs_pending_notification;
                    END IF;
                END IF;

                IF to_regclass('reporting.legacy_tax_reports') IS NOT NULL
                   AND to_regclass('reporting.tax_reports') IS NULL THEN
                    ALTER TABLE reporting.legacy_tax_reports
                        RENAME CONSTRAINT "PK_legacy_tax_reports" TO "PK_tax_reports";
                    ALTER INDEX reporting."IX_legacy_tax_reports_AccountId_Year"
                        RENAME TO "IX_tax_reports_AccountId_Year";
                    ALTER TABLE reporting.legacy_tax_reports
                        RENAME TO tax_reports;
                END IF;

                IF to_regprocedure('reporting.notify_legacy_tax_report_job_pending()') IS NOT NULL
                   AND to_regprocedure('reporting.notify_tax_report_job_pending()') IS NULL THEN
                    ALTER FUNCTION reporting.notify_legacy_tax_report_job_pending()
                        RENAME TO notify_tax_report_job_pending;
                END IF;
            END;
            $$;
            """);
    }
}
