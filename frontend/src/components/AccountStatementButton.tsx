import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useInitiateAccountStatement,
    useAccountStatementStatus,
    useDownloadAccountStatement,
} from "../hooks/useReports";
import ActionButton from "./accounts/ActionButton";

type AccountStatementBtnProps = {
    accountId?: number;
}

export default function AccountStatementBtn({ accountId }: AccountStatementBtnProps) {

    const { t } = useTranslation();

    const [fromDate, setFromDate] = useState("");
    const [toDate, setToDate] = useState("");

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
        startDownload(accountStatementId);
    }, [accountStatementId, isReady, startDownload]);

    const handleClick = () => {
        if (accountId == null || !isRangeValid) return;
        resetDownload();
        initiate.mutate({ accountId, fromDate, toDate });
    };

    return (
        <div className="flex flex-col gap-2 [direction:ltr]">
            <div className="flex items-center gap-2 self-start">
                <ActionButton
                    onClick={handleClick}
                    disabled={accountId == null || !isRangeValid}
                    isPending={isBusy}
                    ariaLabel={t("accounts-route.account-report")}
                    prefixIcon="/icons/file-pdf.svg"
                    title={isBusy ? t("account-statement-button.loading") : t("accounts-route.account-report")}
                />
                <input
                    type="date"
                    value={fromDate}
                    max={toDate || undefined}
                    onChange={(e) => setFromDate(e.target.value)}
                    disabled={isBusy}
                    aria-label={t("account-statement-button.from-label")}
                    className="cursor-pointer font-montserrat text-nordiska-blue p-1 rounded-xl border-2 border-nordiska-blue bg-white disabled:opacity-50 disabled:cursor-not-allowed" />
                <input
                    type="date"
                    value={toDate}
                    min={fromDate || undefined}
                    onChange={(e) => setToDate(e.target.value)}
                    disabled={isBusy}
                    aria-label={t("account-statement-button.to-label")}
                    className="cursor-pointer font-montserrat text-nordiska-blue p-1 rounded-xl border-2 border-nordiska-blue bg-white disabled:opacity-50 disabled:cursor-not-allowed" />
            </div>
            {isFailed && (
                <p role="alert" className="font-montserrat text-red-600">
                    {t("account-statement-button.error")}
                </p>
            )}
        </div>
    );
}
