import { useMutation, useQuery } from "@tanstack/react-query";
import {
    initiateTaxReport,
    checkTaxReportStatus,
    fetchTaxReportPdf,
    initiateAccountStatement,
    checkAccountStatementStatus,
    fetchAccountStatementPdf,
    type ReportStatus,
} from "../services/reportsService";

const TERMINAL_STATUSES: ReportStatus[] = ["Completed", "Failed"];
const POLL_INTERVAL_MS = 2000;

function getPollInterval(isError: boolean, status: ReportStatus | null | undefined): number | false {
    if (isError) return false;
    return status && TERMINAL_STATUSES.includes(status) ? false : POLL_INTERVAL_MS;
}

interface InitiateTaxReportVars {
    accountId: number;
    year?: number;
}

interface InitiateAccountStatementVars {
    accountId: number;
    fromDate: string;
    toDate: string;
}

export function useInitiateTaxReport() {
    return useMutation({
        mutationFn: ({ accountId, year }: InitiateTaxReportVars) =>
            initiateTaxReport(accountId, year),
    });
}

export function useReportStatus(jobId: number | undefined) {
    return useQuery({
        queryKey: ["reports", "tax", "jobs", jobId],
        queryFn: () => checkTaxReportStatus(jobId!),
        enabled: jobId !== undefined,
        refetchInterval: (query) =>
            getPollInterval(query.state.status === "error", query.state.data?.status),
    });
}

export function useDownloadReport() {
    return useMutation({
        mutationFn: async (taxReportId: number) =>
            URL.createObjectURL(await fetchTaxReportPdf(taxReportId)),
    });
}

export function useInitiateAccountStatement() {
    return useMutation({
        mutationFn: ({ accountId, fromDate, toDate }: InitiateAccountStatementVars) =>
            initiateAccountStatement(accountId, fromDate, toDate),
    });
}

export function useAccountStatementStatus(jobId: number | undefined) {
    return useQuery({
        queryKey: ["reports", "account-statements", "jobs", jobId],
        queryFn: () => checkAccountStatementStatus(jobId!),
        enabled: jobId !== undefined,
        refetchInterval: (query) =>
            getPollInterval(query.state.status === "error", query.state.data?.status),
    });
}

export function useDownloadAccountStatement() {
    return useMutation({
        mutationFn: async (accountStatementId: number) =>
            URL.createObjectURL(await fetchAccountStatementPdf(accountStatementId)),
    });
}
