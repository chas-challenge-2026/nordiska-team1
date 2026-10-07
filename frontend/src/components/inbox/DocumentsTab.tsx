import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useArchivedDocuments, useDownloadInboxDocument } from "../../hooks/useInbox";
import { formatDate } from "../../utils/date";
import PagePagination from "../PagePagination";

type DocumentsTabProps = {
    page: number;
    onPageChange: (page: number) => void;
};

const btn = "cursor-pointer rounded bg-primary-blue px-4 py-2 font-semibold text-white disabled:cursor-default disabled:opacity-40";
const currentYear = new Date().getFullYear();
const years = Array.from({ length: 5 }, (_, i) => currentYear - i);

export default function DocumentsTab({ page, onPageChange }: DocumentsTabProps) {
    const { t } = useTranslation();
    const [year, setYear] = useState<number | undefined>(undefined);
    const documents = useArchivedDocuments(page, 10, year);
    const download = useDownloadInboxDocument();
    const data = documents.data;

    return (
        <div className="bg-white shadow-card">
            <div className="bg-dark-navy px-4 py-3 text-white">
                <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.archive")}</span>
                <h2 className="font-bold">{t("inbox.documents")}</h2>
            </div>

            <div className="flex items-center gap-2 p-4">
                <label htmlFor="doc-year" className="text-xs font-semibold uppercase text-secondary">
                    {t("inbox.documents-tab.year")}
                </label>
                <select
                    id="doc-year"
                    value={year ?? ""}
                    onChange={(event) => {
                        setYear(event.target.value ? Number(event.target.value) : undefined);
                        onPageChange(1);
                    }}
                    className="border border-secondary p-2"
                >
                    <option value="">{t("inbox.documents-tab.all-years")}</option>
                    {years.map((y) => <option key={y} value={y}>{y}</option>)}
                </select>
            </div>

            {documents.isPending && <p role="status" className="px-4 py-3 text-secondary">{t("inbox.loading")}</p>}

            {documents.isError && (
                <div className="flex items-center justify-between px-4 py-3">
                    <span role="alert" className="text-error">{t("inbox.error.load")}</span>
                    <button type="button" onClick={() => documents.refetch()} className="cursor-pointer text-primary-blue underline">
                        {t("inbox.try-again")}
                    </button>
                </div>
            )}

            {data && data.items.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.documents")}</p>
            )}

            {download.isError && <p role="alert" className="px-4 py-2 text-error">{t("inbox.error.action")}</p>}

            {data && data.items.length > 0 && (
                <ul>
                    {data.items.map((doc) => {
                        const name = doc.title ?? doc.fileName ?? t("inbox.untitled");
                        const downloading = download.isPending && download.variables === doc.documentId;
                        return (
                            <li
                                key={doc.documentId}
                                className="flex flex-wrap items-center justify-between gap-2 border-t border-light-blue-accent/50 px-4 py-3"
                            >
                                <span className={`min-w-0 ${doc.hasBeenOpened ? "" : "font-bold"}`}>
                                    <span className="block text-xs font-normal text-secondary">
                                        {[doc.documentType, formatDate(doc.publishedAt), `${Math.ceil(doc.fileSizeBytes / 1024)} kB`]
                                            .filter(Boolean)
                                            .join(" · ")}
                                    </span>
                                    <span className="block break-words">
                                        {!doc.hasBeenOpened && <span className="sr-only">{t("inbox.new-prefix")}</span>}
                                        {name}
                                    </span>
                                </span>
                                <button
                                    type="button"
                                    className={btn}
                                    disabled={download.isPending}
                                    aria-label={t("inbox.download-aria", { name })}
                                    onClick={() => download.mutate(doc.documentId)}
                                >
                                    {downloading ? t("inbox.downloading") : t("inbox.download")}
                                </button>
                            </li>
                        );
                    })}
                </ul>
            )}

            {data && data.totalPages > 1 && (
                <div className="border-t border-light-blue-accent/50 p-4">
                    <PagePagination
                        page={data.page}
                        totalPages={data.totalPages}
                        hasPreviousPage={data.hasPreviousPage}
                        hasNextPage={data.hasNextPage}
                        onPrevious={() => onPageChange(data.page - 1)}
                        onNext={() => onPageChange(data.page + 1)}
                        onPageChange={onPageChange}
                        isDisabled={documents.isPlaceholderData}
                    />
                </div>
            )}
        </div>
    );
}
