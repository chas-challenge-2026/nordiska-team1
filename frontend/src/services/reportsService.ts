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

async function fetchPdf(path: string): Promise<Blob> {
    const res = await axiosInstance.get<Blob>(path, {
        responseType: "blob",
        headers: { Accept: "application/pdf" },
        timeout: 60000,
    });
    return res.data;
}

export async function initiateTaxReport(accountId: number, taxYear?: number): Promise<TaxReportResponse> {
    const res = await axiosInstance.post("/reports/tax", { accountId, taxYear });
    return res.data;
}

export async function checkTaxReportStatus(jobId: number): Promise<TaxReportJobStatus> {
    const res = await axiosInstance.get(`/reports/tax/jobs/${jobId}`);
    return res.data;
}

export function fetchTaxReportPdf(taxReportId: number): Promise<Blob> {
    return fetchPdf(`/reports/tax/${taxReportId}/pdf`);
}

export async function initiateAccountStatement(accountId: number, fromDate: string, toDate: string): Promise<AccountStatementResponse> {
    const res = await axiosInstance.post("/reports/account-statements", { accountId, fromDate, toDate });
    return res.data;
}

export async function checkAccountStatementStatus(jobId: number): Promise<AccountStatementJobStatus> {
    const res = await axiosInstance.get(`/reports/account-statements/jobs/${jobId}`);
    return res.data;
}

export async function fetchAccountStatementPdf(accountStatementId: number): Promise<Blob> {
    return fetchPdf(`/reports/account-statements/${accountStatementId}/pdf`);
}
