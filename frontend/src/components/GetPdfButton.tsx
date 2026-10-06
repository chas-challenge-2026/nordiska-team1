import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useInitiateTaxReport,
    useReportStatus,
    useDownloadReport,
} from "../hooks/useReports";

type GetPdfBtnProps = {
    accountId?: number;
    year?: number;
}

export default function GetPdfBtn({ accountId, year }: GetPdfBtnProps) {

    const { t } = useTranslation();

    const currentYear = new Date().getFullYear();
    const years = Array.from({ length: 10 }, (_, i) => currentYear - i);
    const [selectedYear, setSelectedYear] = useState(year ?? currentYear);

    const initiate = useInitiateTaxReport();
    const jobId = initiate.data?.jobId ?? undefined;
    const status = useReportStatus(jobId);
    const {
        mutate: startDownload,
        isPending: isDownloading,
        isError: downloadFailed,
    } = useDownloadReport();
    const downloadedJobId = useRef<string | null>(null);

    const reportStatus = status.data?.status;
    const isReady = reportStatus === "Done";
    const isFailed =
        initiate.isError || status.isError || reportStatus === "Failed" || downloadFailed;
    const isBusy =
        initiate.isPending || (!!jobId && !isReady && !isFailed) || isDownloading;

    useEffect(() => {
        if (!jobId || !isReady || downloadedJobId.current === jobId) return;
        downloadedJobId.current = jobId;
        startDownload(jobId);
    }, [jobId, isReady, startDownload]);

    const handleClick = () => {
        if (accountId == null) return;
        initiate.mutate({ accountId, year: selectedYear });
    };

    return (
        <>
            <div className="flex items-center gap-2 self-start">
                <button
                    onClick={handleClick}
                    disabled={isBusy || accountId == null}
                    className="cursor-pointer bg-nordiska-blue font-montserrat text-white p-1 w-fit rounded-xl border-2 border-nordiska-blue transition-colors duration-200 hover:bg-white hover:text-nordiska-blue disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-nordiska-blue disabled:hover:text-white">
                    {isBusy ? t("get-pdf-button.loading") : t("get-pdf-button.text")}
                </button>
                <select
                    value={selectedYear}
                    onChange={(e) => setSelectedYear(Number(e.target.value))}
                    disabled={isBusy}
                    aria-label={t("get-pdf-button.year-label")}
                    className="cursor-pointer font-montserrat text-nordiska-blue p-1 rounded-xl border-2 border-nordiska-blue bg-white disabled:opacity-50 disabled:cursor-not-allowed">
                    {years.map((y) => (
                        <option key={y} value={y}>{y}</option>
                    ))}
                </select>
            </div>
            {isFailed && (
                <p role="alert" className="font-montserrat text-red-600">
                    {t("get-pdf-button.error")}
                </p>
            )}
        </>
    );
}
