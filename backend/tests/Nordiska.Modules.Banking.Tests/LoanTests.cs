using Nordiska.Modules.Banking.Domain;

namespace Nordiska.Modules.Banking.Tests;

public class LoanTests
{
    private static readonly DateOnly OpenedAt = new(2026, 1, 1);

    private static Loan CreateLoan(decimal principal = 100000m, decimal rate = 0.0365m)
        => new(1, "LN-TEST", LoanType.Personal, principal, rate, OpenedAt, OpenedAt.AddMonths(24));

    [Fact]
    public void Ctor_StartsActiveWithNothingAccrued()
    {
        var loan = CreateLoan();

        Assert.Equal(LoanStatus.Active, loan.Status);
        Assert.Equal(100000m, loan.OutstandingAmount);
        Assert.Equal(OpenedAt, loan.InterestAccruedThrough);
    }

    [Fact]
    public void CalculateAccruedInterest_UsesActualDaysOver365()
    {
        var loan = CreateLoan();

        // 100 000 * 3.65 % / 365 = 10 kr per day
        var interest = loan.CalculateAccruedInterest(OpenedAt.AddDays(30));

        Assert.Equal(300m, interest);
    }

    [Fact]
    public void CalculateAccruedInterest_DoesNotChangeTheLoan()
    {
        var loan = CreateLoan();

        loan.CalculateAccruedInterest(OpenedAt.AddDays(30));

        Assert.Equal(100000m, loan.OutstandingAmount);
        Assert.Equal(OpenedAt, loan.InterestAccruedThrough);
    }

    [Fact]
    public void AccrueInterest_AddsInterestAndMovesAccruedThrough()
    {
        var loan = CreateLoan();

        loan.AccrueInterest(OpenedAt.AddDays(30));

        Assert.Equal(100300m, loan.OutstandingAmount);
        Assert.Equal(OpenedAt.AddDays(30), loan.InterestAccruedThrough);
    }

    [Fact]
    public void AccrueInterest_SameDayTwice_OnlyAccruesOnce()
    {
        var loan = CreateLoan();

        loan.AccrueInterest(OpenedAt.AddDays(30));
        loan.AccrueInterest(OpenedAt.AddDays(30));

        Assert.Equal(100300m, loan.OutstandingAmount);
    }

    [Fact]
    public void AccrueInterest_DateBeforeAccruedThrough_DoesNothing()
    {
        var loan = CreateLoan();
        loan.AccrueInterest(OpenedAt.AddDays(30));

        loan.AccrueInterest(OpenedAt.AddDays(10));

        Assert.Equal(100300m, loan.OutstandingAmount);
        Assert.Equal(OpenedAt.AddDays(30), loan.InterestAccruedThrough);
    }

    [Fact]
    public void RegisterRepayment_LowersOutstanding()
    {
        var loan = CreateLoan();

        loan.RegisterRepayment(25000m);

        Assert.Equal(75000m, loan.OutstandingAmount);
        Assert.Equal(LoanStatus.Active, loan.Status);
    }

    [Fact]
    public void RegisterRepayment_ExactOutstanding_MarksLoanRepaid()
    {
        var loan = CreateLoan();
        loan.AccrueInterest(OpenedAt.AddDays(30));

        loan.RegisterRepayment(100300m);

        Assert.Equal(0m, loan.OutstandingAmount);
        Assert.Equal(LoanStatus.Repaid, loan.Status);
    }

    [Fact]
    public void RegisterRepayment_MoreThanOutstanding_Throws()
    {
        var loan = CreateLoan();

        Assert.Throws<InvalidOperationException>(() => loan.RegisterRepayment(100000.01m));
        Assert.Equal(100000m, loan.OutstandingAmount);
    }

    [Fact]
    public void RegisterRepayment_OnRepaidLoan_Throws()
    {
        var loan = CreateLoan();
        loan.RegisterRepayment(100000m);

        Assert.Throws<InvalidOperationException>(() => loan.RegisterRepayment(1m));
    }

    [Fact]
    public void CalculateAccruedInterest_OnRepaidLoan_IsZero()
    {
        var loan = CreateLoan();
        loan.RegisterRepayment(100000m);

        Assert.Equal(0m, loan.CalculateAccruedInterest(OpenedAt.AddDays(100)));
    }
}
