public sealed record TaxReportRequest(long accountId, int year);
public sealed record TaxReportResponse(long accountId, int year);


public class TaxReportService
{
    public async Task<TaxReportResponse> GetTaxReport(TaxReportRequest) 
    { 
        
    }


    // validering som  behövs:
    /*
     * 
     * 1. om download url != null
     * 
     */ 


}