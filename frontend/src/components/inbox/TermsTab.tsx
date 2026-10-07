import { useTranslation } from "react-i18next";
import {
    useAcceptPendingTerm,
    useDownloadInboxDocument,
    usePendingTerms,
} from "../../hooks/useInbox";
import { formatDate } from "../../utils/date";
import { toTermViews } from "./inboxAdapters";

const btn = "cursor-pointer rounded bg-primary-blue px-4 py-2 font-semibold text-white disabled:cursor-default disabled:opacity-40";

export default function TermsTab() {
    const { t } = useTranslation();
    const terms = usePendingTerms();
    const accept = useAcceptPendingTerm();
    const download = useDownloadInboxDocument();

    const items = toTermViews(terms.data);

    return (
        <div className="bg-white shadow-card">
            <div className="bg-dark-navy px-4 py-3 text-white">
                <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.terms-tab.to-accept")}</span>
                <h2 className="font-bold">{t("inbox.terms")}</h2>
            </div>

            {(accept.isError || download.isError) && (
                <p role="alert" className="px-4 py-2 text-error">{t("inbox.error.action")}</p>
            )}

            {terms.isPending && <p role="status" className="px-4 py-3 text-secondary">{t("inbox.loading")}</p>}

            {terms.isError && (
                <div className="flex items-center justify-between px-4 py-3">
                    <span role="alert" className="text-error">{t("inbox.error.load")}</span>
                    <button type="button" onClick={() => terms.refetch()} className="cursor-pointer text-primary-blue underline">
                        {t("inbox.try-again")}
                    </button>
                </div>
            )}

            {terms.isSuccess && items.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.terms")}</p>
            )}

            {items.length > 0 && (
                <ul>
                    {items.map((term) => {
                        const title = term.title ?? t("inbox.untitled");
                        const meta = [
                            term.version !== null ? t("inbox.terms-tab.version", { version: term.version }) : null,
                            term.effectiveFrom
                                ? t("inbox.terms-tab.effective-from", { date: formatDate(term.effectiveFrom) })
                                : null,
                        ].filter(Boolean).join(" · ");
                        const accepting = accept.isPending && accept.variables === term.id;
                        const documentId = term.documentId;

                        return (
                            <li
                                key={term.id}
                                className="flex flex-wrap items-center justify-between gap-2 border-t border-light-blue-accent/50 px-4 py-3"
                            >
                                <span className="min-w-0">
                                    {meta && <span className="block text-xs text-secondary">{meta}</span>}
                                    <span className="block break-words font-bold">{title}</span>
                                </span>
                                <span className="flex flex-wrap items-center gap-2">
                                    {documentId !== null && (
                                        <button
                                            type="button"
                                            className="cursor-pointer px-2 py-2 text-primary-blue underline disabled:cursor-default disabled:opacity-40"
                                            disabled={download.isPending}
                                            aria-label={t("inbox.download-aria", { name: title })}
                                            onClick={() => download.mutate(documentId)}
                                        >
                                            {t("inbox.download")}
                                        </button>
                                    )}
                                    <button
                                        type="button"
                                        className={btn}
                                        disabled={accept.isPending}
                                        aria-label={t("inbox.terms-tab.accept-aria", { title })}
                                        onClick={() => accept.mutate(term.id)}
                                    >
                                        {accepting ? t("inbox.terms-tab.accepting") : t("inbox.terms-tab.accept")}
                                    </button>
                                </span>
                            </li>
                        );
                    })}
                </ul>
            )}
        </div>
    );
}
