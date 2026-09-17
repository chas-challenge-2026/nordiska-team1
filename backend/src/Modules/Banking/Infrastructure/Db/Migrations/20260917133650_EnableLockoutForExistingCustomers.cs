using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nordiska.Modules.Banking.Infrastructure.Db.Migrations
{
    /// <inheritdoc />
    public partial class EnableLockoutForExistingCustomers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Customers created before AddIdentitySupport got LockoutEnabled = false from the column default,
            // so the NOR-70 lockout would never apply to them.
            migrationBuilder.Sql(@"UPDATE banking.customers SET ""LockoutEnabled"" = true WHERE ""LockoutEnabled"" = false;");

            // Login now looks customers up through UserManager (normalized columns), so fill them in for old rows.
            migrationBuilder.Sql(@"UPDATE banking.customers SET ""NormalizedEmail"" = UPPER(""Email"") WHERE ""NormalizedEmail"" IS NULL AND ""Email"" IS NOT NULL;");
            migrationBuilder.Sql(@"UPDATE banking.customers SET ""NormalizedUserName"" = UPPER(""UserName"") WHERE ""NormalizedUserName"" IS NULL AND ""UserName"" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data-only migration. We can not know which rows were false before, so nothing to undo.
        }
    }
}
