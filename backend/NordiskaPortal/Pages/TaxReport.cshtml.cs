using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using NordiskaPortal.Services;
using System.Text.Json;

namespace NordiskaPortal.Pages;

public class TaxReportAccountInfo
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = "";
}

public class TaxReportModel : PageModel
{
    private const string FALLBACK_CONN = "Host=db;Port=5432;Database=nordiska;Username=nordiska;Password=nordiska123";

    // Magic number: tax rate hardcoded — should be in config
    private const decimal CAPITAL_TAX_RATE = 0.30m; // 30% kapitalskatt

    private readonly IConfiguration _config;
    private readonly NativePdfGenerator _pdfGenerator;

    public TaxReportModel(IConfiguration config, NativePdfGenerator pdfGenerator)
    {
        _config = config;
        _pdfGenerator = pdfGenerator;
    }

    public List<TaxReportAccountInfo> Accounts { get; set; } = new();

    public IActionResult OnGet()
    {
        string? customerId = HttpContext.Session.GetString("CustomerId");
        if (customerId == null) return RedirectToPage("/Index");

        LoadAccounts(customerId);
        return Page();
    }

    public IActionResult OnPost(int accountId, int year)
    {
        string? customerId = HttpContext.Session.GetString("CustomerId");
        if (customerId == null) return RedirectToPage("/Index");

        LoadAccounts(customerId);

        try
        {
            string connStr = _config.GetConnectionString("DefaultConnection") ?? FALLBACK_CONN;
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            // Load transactions — all of them for this year, in one go (no pagination)
            string sql;
            NpgsqlCommand cmd;

            if (accountId == 0)
            {
                sql = @"
                    SELECT t.id, sa.account_number, t.type, t.amount, t.balance_after, t.created_at,
                           sa.interest_rate
                    FROM transactions t
                    JOIN savings_accounts sa ON sa.id = t.account_id
                    WHERE sa.customer_id = @cid
                      AND EXTRACT(YEAR FROM t.created_at) = @year
                    ORDER BY t.created_at";
                cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("cid", int.Parse(customerId));
                cmd.Parameters.AddWithValue("year", year);
            }
            else
            {
                sql = @"
                    SELECT t.id, sa.account_number, t.type, t.amount, t.balance_after, t.created_at,
                           sa.interest_rate
                    FROM transactions t
                    JOIN savings_accounts sa ON sa.id = t.account_id
                    WHERE sa.id = @accId AND sa.customer_id = @cid
                      AND EXTRACT(YEAR FROM t.created_at) = @year
                    ORDER BY t.created_at";
                cmd = new NpgsqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("accId", accountId);
                cmd.Parameters.AddWithValue("cid", int.Parse(customerId));
                cmd.Parameters.AddWithValue("year", year);
            }

            var transactions = new List<(int Id, string AccNum, string Type, decimal Amount, decimal BalAfter, DateTime Date, decimal Rate)>();
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    transactions.Add((
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetDecimal(3),
                        reader.GetDecimal(4),
                        reader.GetDateTime(5),
                        reader.GetDecimal(6)
                    ));
                }
            }

            decimal totalDeposits = transactions.Where(tx => tx.Type == "deposit").Sum(tx => tx.Amount);
            decimal totalWithdrawals = transactions.Where(tx => tx.Type != "deposit").Sum(tx => tx.Amount);
            decimal estimatedInterest = transactions.Sum(tx => tx.BalAfter * tx.Rate / 365m);
            var report = new
            {
                account_number = accountId == 0
                    ? $"customer-{customerId}"
                    : Accounts.FirstOrDefault(account => account.Id == accountId)?.AccountNumber
                        ?? $"account-{accountId}",
                title = $"Nordiska tax report {year}",
                transactions = transactions.Select(tx => new
                {
                    date = tx.Date.ToString("yyyy-MM-dd"),
                    type = tx.Type,
                    currency = "SEK",
                    amount_minor = decimal.ToInt64(decimal.Round(tx.Amount * 100m, 0))
                }),
                summary_lines = new[]
                {
                    "Summary",
                    $"Total deposits: {totalDeposits:N2} SEK",
                    $"Total withdrawals: {totalWithdrawals:N2} SEK",
                    $"Estimated interest: {estimatedInterest:N2} SEK",
                    $"Capital tax (30%): {estimatedInterest * CAPITAL_TAX_RATE:N2} SEK"
                }
            };
            byte[] fileBytes = _pdfGenerator.Generate(JsonSerializer.Serialize(report));
            string fileName = $"skatteunderlag_{year}_{DateTime.Now:yyyyMMddHHmm}.pdf";

            // Audit: same file append as Deposit, no locking
            try
            {
                System.IO.File.AppendAllText("audit.log",
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} kund {customerId} taxreport {year}\n");
            }
            catch
            {
                // audit is best effort
            }

            return File(fileBytes, "application/pdf", fileName);
        }
        catch
        {
            // Swallow exception — redirect back with no error message
            return RedirectToPage("/Dashboard");
        }
    }

    private void LoadAccounts(string customerId)
    {
        try
        {
            string connStr = _config.GetConnectionString("DefaultConnection") ?? FALLBACK_CONN;
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT id, account_number FROM savings_accounts WHERE customer_id = @cid ORDER BY id",
                conn);
            cmd.Parameters.AddWithValue("cid", int.Parse(customerId));

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                Accounts.Add(new TaxReportAccountInfo
                {
                    Id = reader.GetInt32(0),
                    AccountNumber = reader.GetString(1)
                });
            }
        }
        catch { }
    }
}
