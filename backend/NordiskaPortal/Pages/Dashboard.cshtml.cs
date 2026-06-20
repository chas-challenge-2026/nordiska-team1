using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace NordiskaPortal.Pages;

public class AccountInfo
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public decimal Balance { get; set; }
    public decimal InterestRate { get; set; }
    public string AccountType { get; set; } = "";
    // Business logic directly in the model — no service layer
    public decimal YearlyInterest => Balance * InterestRate;
}

public class TransactionInfo
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public string Type { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DashboardModel : PageModel
{
    private const string FALLBACK_CONN = "Host=db;Port=5432;Database=nordiska;Username=nordiska;Password=nordiska123";
    private readonly IConfiguration _config;

    public DashboardModel(IConfiguration config)
    {
        _config = config;
    }

    public string CustomerName { get; set; } = "";
    public List<AccountInfo> Accounts { get; set; } = new();
    public List<TransactionInfo> RecentTransactions { get; set; } = new();

    public IActionResult OnGet()
    {
        // Session check — only null check, no expiry verification
        string? customerId = HttpContext.Session.GetString("CustomerId");
        if (customerId == null)
        {
            return RedirectToPage("/Index");
        }

        CustomerName = HttpContext.Session.GetString("CustomerName") ?? "Kund";

        string connStr = _config.GetConnectionString("DefaultConnection") ?? FALLBACK_CONN;

        try
        {
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            // Load accounts — raw SQL in PageModel
            string accountSql = @"
                SELECT id, account_number, balance, interest_rate, account_type
                FROM savings_accounts
                WHERE customer_id = @cid
                ORDER BY id";
            using var accCmd = new NpgsqlCommand(accountSql, conn);
            accCmd.Parameters.AddWithValue("cid", int.Parse(customerId));

            using var accReader = accCmd.ExecuteReader();
            while (accReader.Read())
            {
                Accounts.Add(new AccountInfo
                {
                    Id = accReader.GetInt32(0),
                    AccountNumber = accReader.GetString(1),
                    Balance = accReader.GetDecimal(2),
                    InterestRate = accReader.GetDecimal(3),
                    AccountType = accReader.GetString(4)
                });
            }
            accReader.Close();

            // Load recent transactions — separate query, no JOIN optimization
            string txSql = @"
                SELECT t.id, sa.account_number, t.type, t.amount, t.balance_after, t.created_at
                FROM transactions t
                JOIN savings_accounts sa ON sa.id = t.account_id
                WHERE sa.customer_id = @cid
                ORDER BY t.created_at DESC
                LIMIT 10";
            using var txCmd = new NpgsqlCommand(txSql, conn);
            txCmd.Parameters.AddWithValue("cid", int.Parse(customerId));

            using var txReader = txCmd.ExecuteReader();
            while (txReader.Read())
            {
                RecentTransactions.Add(new TransactionInfo
                {
                    Id = txReader.GetInt32(0),
                    AccountNumber = txReader.GetString(1),
                    Type = txReader.GetString(2),
                    Amount = txReader.GetDecimal(3),
                    BalanceAfter = txReader.GetDecimal(4),
                    CreatedAt = txReader.GetDateTime(5)
                });
            }
        }
        catch
        {
            // Swallow — user sees empty dashboard
        }

        return Page();
    }
}
