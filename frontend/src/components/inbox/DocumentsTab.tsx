import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    useArchivedDocuments,
    useDownloadInboxDocument,
    useGeneralDocuments,
} from "../../hooks/inbox/useDocuments";
import type { InboxDocument, GeneralDocument } from "../../types/inbox";
import { formatDate } from "../../utils/date";
import PagePagination from "../PagePagination";
import InboxButton from "./InboxButton";
import { InboxLoading, InboxLoadError } from "./InboxState";

type DocumentsTabProps = {
    page: number;
    onPageChange: (page: number) => void;
};

const currentYear = new Date().getFullYear();
const years = Array.from({ length: 5 }, (_, i) => currentYear - i);

export default function DocumentsTab({ page, onPageChange }: DocumentsTabProps) {
    const { t } = useTranslation();
    const [year, setYear] = useState<number | undefined>(undefined);
    const documents = useArchivedDocuments(page, 10, year);
    const general = useGeneralDocuments();
    const download = useDownloadInboxDocument();
    const data = documents.data;
    const generalItems = general.data ?? [];
    // Personal endpoint also returns general documents; show each only once.
    const generalIds = new Set(generalItems.map((doc) => doc.id));
    const personalItems = (data?.items ?? []).filter((doc) => !generalIds.has(doc.documentId));

    return (
        <div className="bg-white shadow-card">
            <div className="bg-dark-navy px-4 py-3 text-white">
                <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.archive")}</span>
                <h2 className="font-bold">{t("inbox.documents")}</h2>
            </div>

            {download.isError && <p role="alert" className="px-4 py-2 text-error">{t("generic.error-action")}</p>}

            <div className="flex flex-wrap items-center justify-between gap-2 px-4 pt-4">
                <h3 id="docs-personal" className="text-xs font-semibold uppercase text-secondary">
                    {t("inbox.documents-tab.personal")}
                </h3>
                <div className="flex items-center gap-2">
                    <label htmlFor="doc-year" className="text-xs font-semibold uppercase text-secondary">
                        {t("generic.year")}
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
            </div>

            {documents.isPending && <InboxLoading />}
            {documents.isError && <InboxLoadError onRetry={() => documents.refetch()} />}

            {documents.isSuccess && personalItems.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.documents")}</p>
            )}

            {personalItems.length > 0 && (
                <ul aria-labelledby="docs-personal" className="mt-3">
                    {personalItems.map((doc) => (
                        <DocumentRow key={doc.documentId} doc={doc} downloadId={doc.documentId} download={download} />
                    ))}
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

            <h3 id="docs-general" className="mt-4 border-t-2 border-light-blue-accent/50 px-4 pt-4 text-xs font-semibold uppercase text-secondary">
                {t("inbox.documents-tab.general")}
            </h3>

            {general.isPending && <InboxLoading />}
            {general.isError && <InboxLoadError onRetry={() => general.refetch()} />}

            {general.isSuccess && generalItems.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.documents")}</p>
            )}

            {generalItems.length > 0 && (
                <ul aria-labelledby="docs-general" className="mt-3">
                    {generalItems.map((doc) => (
                        <DocumentRow key={doc.id} doc={doc} downloadId={doc.id} download={download} />
                    ))}
                </ul>
            )}
        </div>
    );
}

type DocumentRowProps = {
    doc: InboxDocument | GeneralDocument;
    downloadId: number;
    download: ReturnType<typeof useDownloadInboxDocument>;
};

function DocumentRow({ doc, downloadId, download }: DocumentRowProps) {
    const { t } = useTranslation();
    const name = doc.title ?? doc.fileName ?? t("inbox.untitled");
    const downloading = download.isPending && download.variables === downloadId;

    return (
        <li className="flex flex-wrap items-center justify-between gap-2 border-t border-light-blue-accent/50 px-4 py-3">
            <span className={`min-w-0 ${doc.isRead ? "" : "font-bold"}`}>
                <span className="block text-xs font-normal text-secondary">
                    {[doc.documentType, formatDate(doc.publishedAt), `${Math.ceil(doc.fileSizeBytes / 1024)} kB`]
                        .filter(Boolean)
                        .join(" · ")}
                </span>
                <span className="block break-words">
                    {!doc.isRead && <span className="sr-only">{t("inbox.new-prefix")}</span>}
                    {name}
                </span>
            </span>
            <InboxButton
                disabled={download.isPending}
                aria-label={t("inbox.download-aria", { name })}
                onClick={() => download.mutate(downloadId)}
            >
                {downloading ? t("generic.downloading") : t("generic.download")}
            </InboxButton>
        </li>
    );
}
