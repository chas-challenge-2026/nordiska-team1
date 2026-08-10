using System.Net.Mail;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;

namespace NordiskaPortal.Pages;

public class DepositAccountInfo
{
    public int Id { get; set; }
    public string AccountNumber { get; set; } = "";
    public decimal Balance { get; set; }
}

public class DepositModel : PageModel
{
    private const string FALLBACK_CONN = "Host=db;Port=5432;Database=nordiska;Username=nordiska;Password=nordiska123";

    // Magic numbers — withdrawal limit hardcoded here, not in config or constants file
    private const decimal MAX_WITHDRAWAL = 50000m;
    private const decimal MIN_DEPOSIT = 1m;
    private const decimal MAX_SINGLE_DEPOSIT = 9999999m; // "a million should be enough"

    private readonly IConfiguration _config;

    public DepositModel(IConfiguration config)
    {
        _config = config;
    }

    public string SuccessMessage { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public List<DepositAccountInfo> Accounts { get; set; } = new();

    public IActionResult OnGet()
    {
        string? customerId = HttpContext.Session.GetString("CustomerId");
        if (customerId == null) return RedirectToPage("/Index");

        LoadAccounts(customerId);
        return Page();
    }

    public IActionResult OnPost(int accountId, string transactionType, decimal amount)
    {
        string? customerId = HttpContext.Session.GetString("CustomerId");
        if (customerId == null) return RedirectToPage("/Index");

        LoadAccounts(customerId);

        // Validation — inline in page handler, should be in a service
        if (amount <= 0)
        {
            ErrorMessage = "Beloppet måste vara större än 0.";
            return Page();
        }

        if (transactionType == "withdrawal" && amount > MAX_WITHDRAWAL)
        {
            ErrorMessage = $"Max uttag är {MAX_WITHDRAWAL:N0} kr per transaktion.";
            return Page();
        }

        if (transactionType == "deposit" && amount > MAX_SINGLE_DEPOSIT)
        {
            ErrorMessage = "Beloppet är för stort. Kontakta kundtjänst.";
            return Page();
        }

        try
        {
            string connStr = _config.GetConnectionString("DefaultConnection") ?? FALLBACK_CONN;
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            // RACE CONDITION: No transaction, no row lock.
            // Two concurrent requests will both read the old balance and both succeed,
            // resulting in double-spending or incorrect final balance.

            // First get current balance for logging
            decimal currentBalance = 0;
            using (var balCmd = new NpgsqlCommand(
                "SELECT balance FROM savings_accounts WHERE id = @id AND customer_id = @cid",
                conn))
            {
                balCmd.Parameters.AddWithValue("id", accountId);
                balCmd.Parameters.AddWithValue("cid", int.Parse(customerId));
                var result = balCmd.ExecuteScalar();
                if (result == null)
                {
                    ErrorMessage = "Kontot hittades inte.";
                    return Page();
                }
                currentBalance = (decimal)result;
            }

            if (transactionType == "withdrawal" && currentBalance < amount)
            {
                ErrorMessage = "Otillräckligt saldo.";
                return Page();
            }

            decimal delta = transactionType == "deposit" ? amount : -amount;

            // BUG: No transaction wrapper — if second UPDATE fails, balance is wrong
            // BUG: No SELECT FOR UPDATE — concurrent requests cause race condition
            using (var updateCmd = new NpgsqlCommand(
                "UPDATE savings_accounts SET balance = balance + @delta WHERE id = @id AND customer_id = @cid",
                conn))
            {
                updateCmd.Parameters.AddWithValue("delta", delta);
                updateCmd.Parameters.AddWithValue("id", accountId);
                updateCmd.Parameters.AddWithValue("cid", int.Parse(customerId));
                updateCmd.ExecuteNonQuery();
            }

            // Log the transaction — separate query, not in same transaction
            decimal newBalance = currentBalance + delta;
            using (var txCmd = new NpgsqlCommand(
                "INSERT INTO transactions (account_id, type, amount, balance_after) VALUES (@accId, @type, @amount, @bal)",
                conn))
            {
                txCmd.Parameters.AddWithValue("accId", accountId);
                txCmd.Parameters.AddWithValue("type", transactionType);
                txCmd.Parameters.AddWithValue("amount", amount);
                txCmd.Parameters.AddWithValue("bal", newBalance);
                txCmd.ExecuteNonQuery();
            }

            string verb = transactionType == "deposit" ? "Insättning" : "Uttag";
            SuccessMessage = $"{verb} på {amount:N2} kr genomförd. Nytt saldo: {newBalance:N2} kr.";

            // Confirmation mail sent right here in the handler - no queue, no retry
            try
            {
                string? email = null;
                using (var mailCmd = new NpgsqlCommand(
                    "SELECT email FROM customers WHERE id = @cid", conn))
                {
                    mailCmd.Parameters.AddWithValue("cid", int.Parse(customerId));
                    email = mailCmd.ExecuteScalar() as string;
                }

                if (email != null)
                {
                    var smtp = new SmtpClient(
                        _config["Smtp:Host"] ?? "localhost",
                        int.Parse(_config["Smtp:Port"] ?? "25"));
                    smtp.Send("noreply@nordiskasparbanken.se", email,
                        $"{verb} genomförd",
                        $"{verb} på {amount:N2} kr har genomförts. Nytt saldo: {newBalance:N2} kr.");
                }
            }
            catch
            {
                // mail is not critical
            }

            // Reload accounts to show updated balance
            LoadAccounts(customerId);
        }
        catch
        {
            // Silent swallow — user sees generic error
            ErrorMessage = "Transaktionen misslyckades. Försök igen.";
        }

        return Page();
    }

    private void LoadAccounts(string customerId)
    {
        try
        {
            string connStr = _config.GetConnectionString("DefaultConnection") ?? FALLBACK_CONN;
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            using var cmd = new NpgsqlCommand(
                "SELECT id, account_number, balance FROM savings_accounts WHERE customer_id = @cid ORDER BY id",
                conn);
            cmd.Parameters.AddWithValue("cid", int.Parse(customerId));

            using var reader = cmd.ExecuteReader();
            Accounts.Clear();
            while (reader.Read())
            {
                Accounts.Add(new DepositAccountInfo
                {
                    Id = reader.GetInt32(0),
                    AccountNumber = reader.GetString(1),
                    Balance = reader.GetDecimal(2)
                });
            }
        }
        catch
        {
            // Swallow
        }
    }
}
