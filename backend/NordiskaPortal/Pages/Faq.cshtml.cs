using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NordiskaPortal.Pages;

public class FaqItem
{
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Category { get; set; } = "";
    public string[] Keywords { get; set; } = Array.Empty<string>();
}

public class FaqModel : PageModel
{
    // FAQ list hardcoded here, should live in a database
    private static readonly List<FaqItem> FAQ = new()
    {
        new FaqItem
        {
            Question = "När betalas räntan ut?",
            Answer = "Räntan beräknas dagligen och betalas ut den 31 december varje år.",
            Category = "Ränta",
            Keywords = new[] { "ränta", "räntan", "räntesats", "procent", "utbetalning" }
        },
        new FaqItem
        {
            Question = "Hur gör jag en insättning?",
            Answer = "Logga in och välj Insättning / Uttag i menyn. Ange belopp och bekräfta. Pengarna syns direkt på kontot.",
            Category = "Insättning",
            Keywords = new[] { "insättning", "insättningar", "sätta", "pengar", "överföring" }
        },
        new FaqItem
        {
            Question = "Hur gör jag ett uttag?",
            Answer = "Logga in och välj Insättning / Uttag i menyn, välj Uttag som typ. Max 50 000 kr per transaktion.",
            Category = "Uttag",
            Keywords = new[] { "uttag", "uttaget", "ta", "gräns" }
        },
        new FaqItem
        {
            Question = "Var hittar jag mitt årsbesked?",
            Answer = "Årsbeskedet ingår i skatteunderlaget. Välj Skatteunderlag i menyn och ladda ner filen för det år du vill se.",
            Category = "Rapporter",
            Keywords = new[] { "årsbesked", "årsbeskedet", "skatt", "deklaration", "deklarationen" }
        },
        new FaqItem
        {
            Question = "Hur får jag ett kontoutdrag?",
            Answer = "Dina senaste transaktioner visas på Mitt konto. Fullständigt kontoutdrag ingår i skatteunderlaget.",
            Category = "Rapporter",
            Keywords = new[] { "kontoutdrag", "utdrag", "transaktioner", "historik" }
        },
        new FaqItem
        {
            Question = "Var hittar jag villkoren för mitt sparkonto?",
            Answer = "Villkoren finns på sidan Insättning / Uttag under Information. Fullständiga avtalsvillkor skickas per post.",
            Category = "Villkor",
            Keywords = new[] { "villkor", "villkoren", "avtal", "regler" }
        },
        new FaqItem
        {
            Question = "Hur öppnar jag ett nytt sparkonto?",
            Answer = "Kontakta kundtjänst på 08-123 456 78 så hjälper vi dig att öppna ett nytt sparkonto.",
            Category = "Konto",
            Keywords = new[] { "öppna", "öppnar", "nytt", "konto", "skapa" }
        },
        new FaqItem
        {
            Question = "Hur avslutar jag mitt sparkonto?",
            Answer = "Ta ut hela saldot och kontakta sedan kundtjänst på 08-123 456 78 för att avsluta kontot.",
            Category = "Konto",
            Keywords = new[] { "avsluta", "avslutar", "stänga", "säga", "upp" }
        }
    };

    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public string MatchedQuestion { get; set; } = "";
    public bool Searched { get; set; }

    public IActionResult OnGet()
    {
        string? customerId = HttpContext.Session.GetString("CustomerId");
        if (customerId == null) return RedirectToPage("/Index");

        return Page();
    }

    public IActionResult OnPost(string question)
    {
        string? customerId = HttpContext.Session.GetString("CustomerId");
        if (customerId == null) return RedirectToPage("/Index");

        Searched = true;
        Question = question ?? "";

        // Keyword grep - first entry with any shared word wins, no ranking
        string[] words = Question.ToLower().Split(' ');
        foreach (var item in FAQ)
        {
            foreach (var word in words)
            {
                if (item.Keywords.Contains(word))
                {
                    MatchedQuestion = item.Question;
                    Answer = item.Answer;
                    return Page();
                }
            }
        }

        Answer = "Vi hittade tyvärr inget svar på din fråga. Kontakta kundtjänst på 08-123 456 78.";
        return Page();
    }
}
