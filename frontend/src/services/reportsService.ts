import axiosInstance from "./axiosInstance";

export type ReportStatus = "Pending" | "Processing" | "Done" | "Failed";

interface ReportResponse {
    jobId: string | null;
    status: ReportStatus | null;
    createdAt: string;
    downloadUrl: string | null;
    error: string | null;
}

export async function initiateTaxReport(accountId: number, year?: number): Promise<ReportResponse> {
    const res = await axiosInstance.post("/reports/tax-report", { accountId, year });
    return res.data;
}

export async function checkReportStatus(jobId: string): Promise<ReportResponse> {
    const res = await axiosInstance.get(`/reports/jobs/${jobId}`);
    return res.data;
}

export async function downloadReport(jobId: string): Promise<void> {
    const res = await axiosInstance.get<Blob>(
        `/reports/jobs/${encodeURIComponent(jobId)}/download`,
        {
            responseType: "blob",
            headers: { Accept: "application/pdf" },
            timeout: 60000,
        }
    );

    const url = URL.createObjectURL(res.data);
    const link = document.createElement("a");
    link.href = url;
    link.download = `tax-report-${jobId}.pdf`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
}
