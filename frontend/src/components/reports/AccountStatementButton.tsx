import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useInitiateAccountStatement,
    useAccountStatementStatus,
    useDownloadAccountStatement,
} from "../hooks/useReports";
import Modal from "./modals/Modal";
import ActionButton from "./accounts/ActionButton";

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

    useEffect(() => {
        if (accountStatementId === undefined || !isReady || downloadedStatementId.current === accountStatementId) return;
        downloadedStatementId.current = accountStatementId;
        startDownload(accountStatementId, { onSuccess: () => setIsOpen(false) });
    }, [accountStatementId, isReady, startDownload]);

    const handleClick = () => {
        if (accountId == null || !isRangeValid) return;
        resetDownload();
        initiate.mutate({ accountId, fromDate, toDate });
    };

    return (
        <>
            <ActionButton
                onClick={() => setIsOpen(true)}
                disabled={accountId == null}
                ariaLabel={t("accounts-route.account-report")}
                prefixIcon="/icons/file-pdf.svg"
                title={t("accounts-route.account-report")}
            />
            <Modal
                isOpen={isOpen}
                onClose={() => setIsOpen(false)}
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
                                onChange={(e) => setFromDate(e.target.value)}
                                disabled={isBusy}
                                className="min-h-11 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-normal text-dark-navy focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue disabled:cursor-not-allowed disabled:opacity-50" />
                        </label>
                        <label className="flex flex-col gap-1 text-sm font-semibold text-dark-navy">
                            {t("generic.to-date")}
                            <input
                                type="date"
                                value={toDate}
                                min={fromDate || undefined}
                                onChange={(e) => setToDate(e.target.value)}
                                disabled={isBusy}
                                className="min-h-11 rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm font-normal text-dark-navy focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue disabled:cursor-not-allowed disabled:opacity-50" />
                        </label>
                    </div>
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
                        <ActionButton
                            onClick={handleClick}
                            disabled={!isRangeValid}
                            isPending={isBusy}
                            prefixIcon="/icons/file-pdf.svg"
                            title={isBusy ? t("generic.loading") : t("generic.download")}
                        />
                    </div>
                </div>
            </Modal>
        </>
    );
}
