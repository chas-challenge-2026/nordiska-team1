using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Faq.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class PruneLegacyFaqCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Ensure nordiska_api has DELETE permission on faq.faq_entries if the role exists
            migrationBuilder.Sql("DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'nordiska_api') THEN GRANT DELETE ON TABLE faq.faq_entries TO nordiska_api; END IF; END $$;");

            // 2. Remove decommissioned categories from database
            migrationBuilder.Sql("DELETE FROM faq.faq_entries WHERE \"Category\" IN ('Nordiska app', 'Nordiska App', 'Lån', 'Loans');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
