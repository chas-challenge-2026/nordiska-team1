using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Npgsql;
using System.Security.Cryptography;
using System.Text;

namespace NordiskaPortal.Pages;

public class IndexModel : PageModel
{
    // Hardcoded fallback — "just in case config doesn't load"
    private const string FALLBACK_CONN = "Host=db;Port=5432;Database=nordiska;Username=nordiska;Password=nordiska123";

    private readonly IConfiguration _config;

    public IndexModel(IConfiguration config)
    {
        _config = config;
    }

    public string ErrorMessage { get; set; } = "";
    public string Email { get; set; } = "";

    public IActionResult OnGet()
    {
        // If already logged in just send them to dashboard
        if (HttpContext.Session.GetString("CustomerId") != null)
        {
            return RedirectToPage("/Dashboard");
        }
        return Page();
    }

    public IActionResult OnPost(string email, string password)
    {
        Email = email;

        // MD5 the password directly — this is "secure enough" for a savings bank
        string md5Hash = ComputeMd5(password);

        try
        {
            string connStr = _config.GetConnectionString("DefaultConnection") ?? FALLBACK_CONN;
            using var conn = new NpgsqlConnection(connStr);
            conn.Open();

            // Raw SQL with MD5 comparison — no parameterized query for the hash (it's already hashed so it's fine, right?)
            string sql = $"SELECT id, name FROM customers WHERE email = @email AND password_md5 = '{md5Hash}'";
            using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("email", email);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                int customerId = reader.GetInt32(0);
                string customerName = reader.GetString(1);

                // Store everything in session — no real auth token
                HttpContext.Session.SetString("CustomerId", customerId.ToString());
                HttpContext.Session.SetString("CustomerName", customerName);

                return RedirectToPage("/Dashboard");
            }
            else
            {
                ErrorMessage = "Fel e-postadress eller lösenord. Försök igen.";
                return Page();
            }
        }
        catch
        {
            // Swallow exception — user just sees error message
            ErrorMessage = "Ett fel uppstod. Försök igen senare.";
            return Page();
        }
    }

    private string ComputeMd5(string input)
    {
        using var md5 = MD5.Create();
        byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder();
        foreach (byte b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
