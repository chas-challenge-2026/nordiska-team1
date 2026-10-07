import axiosInstance from "./axiosInstance";

export type ReportStatus = "Pending" | "Processing" | "Completed" | "Failed";

interface TaxReportResponse {
    taxReportId: number;
    jobId: number;
    status: ReportStatus | null;
    createdAt: string;
}

interface TaxReportJobStatus extends TaxReportResponse {
    completedAt: string | null;
    error: string | null;
}

interface AccountStatementResponse {
    accountStatementId: number;
    jobId: number;
    status: ReportStatus | null;
    createdAt: string;
}

interface AccountStatementJobStatus extends AccountStatementResponse {
    completedAt: string | null;
    error: string | null;
}

export async function initiateTaxReport(accountId: number, taxYear?: number): Promise<TaxReportResponse> {
    const res = await axiosInstance.post("/reports/tax", { accountId, taxYear });
    return res.data;
}

export async function checkTaxReportStatus(jobId: number): Promise<TaxReportJobStatus> {
    const res = await axiosInstance.get(`/reports/tax/jobs/${jobId}`);
    return res.data;
}

export async function downloadTaxReport(taxReportId: number): Promise<void> {
    const res = await axiosInstance.get<Blob>(
        `/reports/tax/jobs/${taxReportId}/pdf`,
        {
            responseType: "blob",
            headers: { Accept: "application/pdf" },
            timeout: 60000,
        }
    );

    const url = URL.createObjectURL(res.data);
    const link = document.createElement("a");
    link.href = url;
    link.download = `tax-report-${taxReportId}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}

export async function initiateAccountStatement(accountId: number, fromDate: string, toDate: string): Promise<AccountStatementResponse> {
    const res = await axiosInstance.post("/reports/account-statements", {accountId, fromDate, toDate});
    return res.data;
}

export async function checkAccountStatementStatus(jobId: number): Promise<AccountStatementJobStatus> {
    const res = await axiosInstance.get(`/reports/account-statements/jobs/${jobId}`);
    return res.data;
}

export async function downloadAccountStatement(accountStatementId: number): Promise<void> {
    const res = await axiosInstance.get<Blob>(
        `/reports/account-statements/jobs/${accountStatementId}/pdf`,
        {
            responseType: "blob",
            headers: { Accept: "application/pdf" },
            timeout: 60000,
        }
    );

    const url = URL.createObjectURL(res.data);
    const link = document.createElement("a");
    link.href = url;
    link.download = `account-statement-${accountStatementId}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}
