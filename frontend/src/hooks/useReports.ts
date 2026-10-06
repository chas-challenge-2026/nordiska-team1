import { useMutation, useQuery } from "@tanstack/react-query";
import { initiateTaxReport, checkReportStatus, downloadReport, type ReportStatus } from "../services/reportsService";

const TERMINAL_STATUSES: ReportStatus[] = ["Done", "Failed"];
const POLL_INTERVAL_MS = 2000;

interface InitiateTaxReportVars {
    accountId: number;
    year?: number;
}

export function useInitiateTaxReport() {
    return useMutation({
        mutationFn: ({ accountId, year }: InitiateTaxReportVars) =>
            initiateTaxReport(accountId, year),
    });
}

export function useReportStatus(jobId: string | undefined) {
    return useQuery({
        queryKey: ["reports", "jobs", jobId],
        queryFn: () => checkReportStatus(jobId!),
        enabled: !!jobId,
        refetchInterval: (query) => {
            if (query.state.status === "error") return false;
            const status = query.state.data?.status;
            return status && TERMINAL_STATUSES.includes(status) ? false : POLL_INTERVAL_MS;
        },
    });
}

export function useDownloadReport() {
    return useMutation({ mutationFn: downloadReport });
}
