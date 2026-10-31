import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useInitiateAccountStatement,
    useAccountStatementStatus,
    useDownloadAccountStatement,
} from "../../hooks/useReports";
import Modal from "../modals/Modal";
import ActionButton from "../accounts/ActionButton";
import { ReportTimeoutError } from "../../services/reportsTimeout";

type AccountStatementBtnProps = {
    accountId?: number;
}

export default function AccountStatementBtn({ accountId }: AccountStatementBtnProps) {

    const { t } = useTranslation();

    const [fromDate, setFromDate] = useState("");
    const [toDate, setToDate] = useState("");
    const [isOpen, setIsOpen] = useState(false);

    const initiate = useInitiateAccountStatement();
    const jobId = initiate.data?.jobId;
    const accountStatementId = initiate.data?.accountStatementId;
    const status = useAccountStatementStatus(jobId);
    const {
        mutate: startDownload,
        reset: resetDownload,
        data: pdfUrl,
        isPending: isDownloading,
        isError: downloadFailed,
    } = useDownloadAccountStatement();
    const downloadedStatementId = useRef<number | null>(null);

    const reportStatus = status.data?.status;
    const isReady = reportStatus === "Completed";
    const isFailed =
        initiate.isError || status.isError || reportStatus === "Failed" || downloadFailed;
    const isBusy =
        initiate.isPending || (jobId !== undefined && !isReady && !isFailed) || isDownloading;
    const isRangeValid = fromDate !== "" && toDate !== "" && fromDate <= toDate;
    const isTimedOut = status.error instanceof ReportTimeoutError;

    useEffect(() => {
        if (accountStatementId === undefined || !isReady || downloadedStatementId.current === accountStatementId) return;
        downloadedStatementId.current = accountStatementId;
        startDownload(accountStatementId);
    }, [accountStatementId, isReady, startDownload]);


    useEffect(() => {
        if (!pdfUrl) return;
        return () => {
            URL.revokeObjectURL(pdfUrl);
        };
    }, [pdfUrl]);

    const handleClick = () => {
        if (accountId == null || !isRangeValid) return;
        resetDownload();
        initiate.mutate({ accountId, fromDate, toDate });
    };

    const handleClose = () => {
        setIsOpen(false);
        setFromDate("");
        setToDate("");
        resetDownload();
        initiate.reset();
    };

    return (
        <>
            <button
                type="button"
                onClick={() => setIsOpen(true)}
                disabled={accountId == null}
                aria-label={t("accounts-route.account-report")}
                className="inline-flex min-w-0 flex-1 items-center justify-center rounded-lg px-2.5 py-1.5 text-xs font-semibold text-white transition cursor-pointer bg-primary-blue hover:bg-primary-hover sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:bg-primary-blue"
            >
                <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/file-pdf.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5" />
                {t("accounts-route.account-report")}
            </button>
            
            <Modal
                isOpen={isOpen}
                onClose={handleClose}
                title={t("accounts-route.account-report")}
            >
                <div className="flex flex-col gap-4 p-5 sm:p-6 [direction:ltr]">
                    <div className="grid gap-3 sm:grid-cols-2">
                        <label className="flex flex-col gap-1 text-sm font-semibold text-dark-navy">
                            {t("generic.from-date")}
                            <input
                                type="date"
                                value={fromDate}
                                max={toDate || undefined}
                                onChange={(e) => {
                                    setFromDate(e.target.value);
                                    resetDownload();
                                    initiate.reset();
                                }}
                                disabled={isBusy}
                                className="min-h-11 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-normal text-dark-navy focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue disabled:cursor-not-allowed disabled:opacity-50" />
                        </label>
                        <label className="flex flex-col gap-1 text-sm font-semibold text-dark-navy">
                            {t("generic.to-date")}
                            <input
                                type="date"
                                value={toDate}
                                min={fromDate || undefined}
                                onChange={(e) => {
                                    setToDate(e.target.value);
                                    resetDownload();
                                    initiate.reset();
                                }}
                                disabled={isBusy}
                                className="min-h-11 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-normal text-dark-navy focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue disabled:cursor-not-allowed disabled:opacity-50" />
                        </label>
                    </div>
                    {isFailed && (
                        <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm font-medium text-red-700">
                            {t(isTimedOut ? "errors.timeout" : "errors.report")}
                        </p>
                    )}
                    <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
                        <button
                            type="button"
                            onClick={handleClose}
                            className="min-h-11 rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-semibold text-dark-navy transition cursor-pointer hover:bg-gray-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2"
                        >
                            {t("generic.cancel")}
                        </button>
                        {pdfUrl ? (
                            <>
                                <a
                                    href={pdfUrl}
                                    download={`${t("accounts-route.account-statement-filename", { from: fromDate, to: toDate })}.pdf`}
                                    className="inline-flex min-h-11 items-center justify-center rounded-lg border border-primary-blue bg-white px-4 py-2.5 text-sm font-semibold text-primary-blue transition cursor-pointer hover:bg-primary-blue/10 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2"
                                >
                                    {t("generic.download")}
                                </a>
                                <a
                                    href={pdfUrl}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    className="inline-flex min-h-11 items-center justify-center rounded-lg bg-primary-blue px-4 py-2.5 text-sm font-semibold text-white transition cursor-pointer hover:bg-nordiska-blue focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2"
                                >
                                    <span aria-hidden="true" className="mr-2 h-6 w-6 bg-white mask-[url('/icons/file-pdf.svg')] mask-contain mask-center mask-no-repeat" />
                                    {t("accounts-route.show-statement")}
                                </a>
                            </>
                        ) : (
                            <ActionButton
                                onClick={handleClick}
                                disabled={!isRangeValid}
                                isPending={isBusy}
                                prefixIcon="/icons/file-pdf.svg"
                                title={isBusy ? t("generic.loading") : t("accounts-route.generate-statement")}
                            />
                        )}
                    </div>
                </div>
            </Modal>
        </>
    );
}
