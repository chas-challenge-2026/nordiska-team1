using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Text;

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

    public TaxReportModel(IConfiguration config)
    {
        _config = config;
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

            // SYNCHRONOUS PDF GENERATION — blocks the request thread
            // Thread.Sleep(50) per transaction simulates "rendering" work
            // This would time out for year-end batch (10,000 customers × many transactions)
            var sb = new StringBuilder();
            sb.AppendLine("NORDISKA SPARBANKEN AB");
            sb.AppendLine("Organisationsnummer: 556789-1234");
            sb.AppendLine("Skatteunderlag / Kontrolluppgift");
            sb.AppendLine("====================================");
            sb.AppendLine($"Kund-ID: {customerId}");
            sb.AppendLine($"Kundnamn: {HttpContext.Session.GetString("CustomerName")}");
            sb.AppendLine($"År: {year}");
            sb.AppendLine($"Genererat: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine("TRANSAKTIONER");
            sb.AppendLine("------------------------------------");

            decimal totalDeposits = 0;
            decimal totalWithdrawals = 0;
            decimal estimatedInterest = 0;

            foreach (var tx in transactions)
            {
                // Fake synchronous "rendering" delay per transaction — intentional bottleneck
                Thread.Sleep(50);

                string typeLabel = tx.Type == "deposit" ? "Insättning" : "Uttag";
                sb.AppendLine($"{tx.Date:yyyy-MM-dd}  {tx.AccNum,-15}  {typeLabel,-12}  {tx.Amount,12:N2} kr  Saldo: {tx.BalAfter,12:N2} kr");

                if (tx.Type == "deposit")
                    totalDeposits += tx.Amount;
                else
                    totalWithdrawals += tx.Amount;

                // Interest estimate: crude approximation, not real accrual
                estimatedInterest += tx.BalAfter * tx.Rate / 365;
            }

            sb.AppendLine();
            sb.AppendLine("SAMMANFATTNING");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"Totala insättningar:   {totalDeposits,12:N2} kr");
            sb.AppendLine($"Totala uttag:          {totalWithdrawals,12:N2} kr");
            sb.AppendLine($"Beräknad ränta {year}:   {estimatedInterest,12:N2} kr");
            sb.AppendLine($"Kapitalskatt (30%):    {estimatedInterest * CAPITAL_TAX_RATE,12:N2} kr");
            sb.AppendLine();
            sb.AppendLine("Detta dokument är genererat automatiskt och utgör underlag");
            sb.AppendLine("för deklaration av kapitalinkomster (K4/K12).");
            sb.AppendLine();
            sb.AppendLine("Nordiska Sparbanken AB, Box 1234, 111 11 Stockholm");
            sb.AppendLine("Tel: 08-123 456 78  |  kundtjanst@nordiska.se");

            // "PDF" is actually plain text with .pdf extension — minimum viable spaghetti
            byte[] fileBytes = Encoding.UTF8.GetBytes(sb.ToString());
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
