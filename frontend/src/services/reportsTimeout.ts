import { type ReportStatus } from "./reportsService";


const REPORT_TIMEOUT_MS = 60_000;

export class ReportTimeoutError extends Error {
    constructor() {
        super("Report generation timed out");
        this.name = "ReportTimeoutError";
    }
}

function parseUtc(iso: string): number {
    return Date.parse(
        iso.replace(/\.(\d+)Z$/, (_, f: string) => `.${f.padEnd(3, "0").slice(0, 3)}Z`),
    );
}

function getServerNow(dateHeader: unknown): number {
    if (typeof dateHeader === "string") {
        const parsed = Date.parse(dateHeader);
        if (!Number.isNaN(parsed)) return parsed;
    }
    return Date.now();
}

export function assertNotTimedOut(
    job: { status: ReportStatus | null; createdAt: string },
    dateHeader: unknown,
): void {
    if (job.status === "Completed" || job.status === "Failed") return;

    const created = parseUtc(job.createdAt);
    if (Number.isNaN(created)) {
        throw new Error(`Unparseable createdAt: ${job.createdAt}`);
    }

    if (getServerNow(dateHeader) - created > REPORT_TIMEOUT_MS) {
        throw new ReportTimeoutError();
    }
}
