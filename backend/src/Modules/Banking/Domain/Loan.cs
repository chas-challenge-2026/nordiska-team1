namespace Nordiska.Modules.Banking.Domain;

public sealed class Loan
{
    public long Id { get; private set; }
    public long CustomerId { get; private set; }
    public string LoanNumber { get; private set; } = string.Empty;
    public LoanType Type { get; private set; }
    public decimal PrincipalAmount { get; private set; }
    public decimal OutstandingAmount { get; private set; }
    public decimal InterestRate { get; private set; }
    public string Currency { get; private set; } = "SEK";
    public LoanStatus Status { get; private set; }
    public DateOnly OpenedAt { get; private set; }
    public DateOnly? MaturityDate { get; private set; }
    private Loan() { }
    public Loan(long customerId,string loanNumber,LoanType type,decimal principalAmount,decimal interestRate,DateOnly openedAt,DateOnly? maturityDate=null,string currency="SEK")
    {if(principalAmount<=0) throw new ArgumentOutOfRangeException(nameof(principalAmount)); CustomerId=customerId; LoanNumber=loanNumber; Type=type; PrincipalAmount=OutstandingAmount=principalAmount; InterestRate=interestRate; Currency=currency; OpenedAt=openedAt; MaturityDate=maturityDate; Status=LoanStatus.Active;}
    public void RegisterRepayment(decimal amount){if(amount<=0) throw new ArgumentOutOfRangeException(nameof(amount)); if(Status!=LoanStatus.Active) throw new InvalidOperationException("Loan is not active."); OutstandingAmount-=amount; if(OutstandingAmount<=0){OutstandingAmount=0; Status=LoanStatus.Repaid;}}
}
public enum LoanType { Personal=1, Mortgage=2 }
public enum LoanStatus { Pending=1, Active=2, Repaid=3, Closed=4, Defaulted=5 }
