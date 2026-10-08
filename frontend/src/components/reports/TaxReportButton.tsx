import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useInitiateTaxReport,
    useReportStatus,
    useDownloadReport,
} from "../../hooks/useReports";
import Modal from "../modals/Modal";
import ActionButton from "../accounts/ActionButton";

type TaxReportBtnProps = {
    accountId?: number;
    year?: number;
}

export default function TaxReportBtn({ accountId, year }: TaxReportBtnProps) {

    const { t } = useTranslation();

    const currentYear = new Date().getFullYear();
    const years = Array.from({ length: 10 }, (_, i) => currentYear - i);
    const [selectedYear, setSelectedYear] = useState(year ?? currentYear);
    const [isOpen, setIsOpen] = useState(false);

    const initiate = useInitiateTaxReport();
    const jobId = initiate.data?.jobId;
    const taxReportId = initiate.data?.taxReportId;
    const status = useReportStatus(jobId);
    const {
        mutate: startDownload,
        reset: resetDownload,
        data: pdfUrl,
        isPending: isDownloading,
        isError: downloadFailed,
    } = useDownloadReport();
    const downloadedReportId = useRef<number | null>(null);

    const reportStatus = status.data?.status;
    const isReady = reportStatus === "Completed";
    const isFailed =
        initiate.isError || status.isError || reportStatus === "Failed" || downloadFailed;
    const isBusy =
        initiate.isPending || (jobId !== undefined && !isReady && !isFailed) || isDownloading;

    useEffect(() => {
        if (taxReportId === undefined || !isReady || downloadedReportId.current === taxReportId) return;
        downloadedReportId.current = taxReportId;
        startDownload(taxReportId);
    }, [taxReportId, isReady, startDownload]);

    useEffect(() => {
        if (!pdfUrl) return;
        return () => {
            URL.revokeObjectURL(pdfUrl);
        };
    }, [pdfUrl]);

    const handleClick = () => {
        if (accountId == null) return;
        resetDownload();
        initiate.mutate({ accountId, year: selectedYear });
    };

    return (
        <>
            <button
                type="button"
                onClick={() => setIsOpen(true)}
                disabled={accountId == null}
                aria-label={t("accounts-route.aria-label.generate-full-report")}
                className="inline-flex min-w-0 flex-1 items-center justify-center rounded-lg px-2.5 py-1.5 text-xs font-semibold text-white transition cursor-pointer bg-primary-blue hover:bg-nordiska-blue sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-primary-blue"
            >
                <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/file-pdf.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5" />
                {t("accounts-route.full-report")}
            </button>
            <Modal
                isOpen={isOpen}
                onClose={() => setIsOpen(false)}
                title={t("accounts-route.full-report")}
            >
                <div className="flex flex-col gap-4 p-5 sm:p-6">
                    <label className="flex flex-col gap-1 text-sm font-semibold text-dark-navy">
                        {t("generic.year")}
                        <select
                            value={selectedYear}
                            onChange={(e) => {
                                setSelectedYear(Number(e.target.value));
                                resetDownload();
                                initiate.reset();
                            }}
                            disabled={isBusy}
                            className="min-h-11 cursor-pointer rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-normal text-dark-navy focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue disabled:cursor-not-allowed disabled:opacity-50">
                            {years.map((y) => (
                                <option key={y} value={y}>{y}</option>
                            ))}
                        </select>
                    </label>
                    {isFailed && (
                        <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm font-medium text-red-700">
                            {t("errors.report")}
                        </p>
                    )}
                    <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
                        <button
                            type="button"
                            onClick={() => setIsOpen(false)}
                            className="min-h-11 rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-semibold text-dark-navy transition cursor-pointer hover:bg-gray-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2"
                        >
                            {t("generic.cancel")}
                        </button>
                        {pdfUrl ? (
                            <>
                                <a
                                    href={pdfUrl}
                                    download={`${t("accounts-route.tax-report-filename", { year: selectedYear })}.pdf`}
                                    className="inline-flex min-h-11 items-center justify-center rounded-lg border border-primary-blue bg-white px-4 py-2.5 text-sm font-semibold text-primary-blue transition cursor-pointer hover:bg-primary-blue/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2"
                                >
                                    {t("generic.download")}
                                </a>
                                <a
                                    href={pdfUrl}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    onClick={() => setIsOpen(false)}
                                    className="inline-flex min-h-11 items-center justify-center rounded-lg bg-primary-blue px-4 py-2.5 text-sm font-semibold text-white transition cursor-pointer hover:bg-nordiska-blue focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2"
                                >
                                    <span aria-hidden="true" className="mr-2 h-6 w-6 bg-white mask-[url('/icons/file-pdf.svg')] mask-contain mask-center mask-no-repeat" />
                                    {t("accounts-route.show-report")}
                                </a>
                            </>
                        ) : (
                            <ActionButton
                                onClick={handleClick}
                                isPending={isBusy}
                                prefixIcon="/icons/file-pdf.svg"
                                title={isBusy ? t("generic.loading") : t("accounts-route.generate-report")}
                            />
                        )}
                    </div>
                </div>
            </Modal>
        </>
    );
}
