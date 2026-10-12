import { useTranslation } from "react-i18next";
import { useAcceptPendingTerm, usePendingTerms } from "../../hooks/inbox/useTerms";
import { useDownloadInboxDocument } from "../../hooks/inbox/useDocuments";
import { formatDate } from "../../utils/date";
import InboxButton from "./InboxButton";

export default function TermsTab() {
    const { t } = useTranslation();
    const terms = usePendingTerms();
    const accept = useAcceptPendingTerm();
    const download = useDownloadInboxDocument();

    const items = terms.data ?? [];

    return (
        <div className="bg-white shadow-card">
            <div className="bg-dark-navy px-4 py-3 text-white">
                <span className="block text-xs uppercase text-light-blue-accent">{t("inbox.terms-tab.to-accept")}</span>
                <h2 className="font-bold">{t("inbox.terms")}</h2>
            </div>

            {(accept.isError || download.isError) && (
                <p role="alert" className="px-4 py-2 text-error">{t("generic.error-action")}</p>
            )}

            {terms.isPending && <p role="status" className="px-4 py-3 text-secondary">{t("generic.loading")}</p>}

            {terms.isError && (
                <div className="flex items-center justify-between px-4 py-3">
                    <span role="alert" className="text-error">{t("generic.error-load")}</span>
                    <InboxButton variant="link" onClick={() => terms.refetch()}>{t("generic.try-again")}</InboxButton>
                </div>
            )}

            {terms.isSuccess && items.length === 0 && (
                <p className="px-4 py-3 text-secondary">{t("inbox.empty.terms")}</p>
            )}

            {items.length > 0 && (
                <ul>
                    {items.map((term) => {
                        const title = term.title ?? term.code ?? t("inbox.untitled");
                        const meta = [
                            t("inbox.terms-tab.version", { version: term.version }),
                            t("inbox.terms-tab.effective-from", { date: formatDate(term.effectiveFrom) }),
                        ].join(" · ");
                        const accepting = accept.isPending && accept.variables === term.termId;

                        return (
                            <li
                                key={term.termId}
                                className="flex flex-wrap items-center justify-between gap-2 border-t border-light-blue-accent/50 px-4 py-3"
                            >
                                <span className="min-w-0">
                                    <span className="block text-xs text-secondary">{meta}</span>
                                    <span className="block break-words font-bold">{title}</span>
                                </span>
                                <span className="flex flex-wrap items-center gap-2">
                                    <InboxButton
                                        variant="link"
                                        className="px-2 py-2"
                                        disabled={download.isPending}
                                        aria-label={t("inbox.download-aria", { name: title })}
                                        onClick={() => download.mutate(term.documentId)}
                                    >
                                        {t("generic.download")}
                                    </InboxButton>
                                    <InboxButton
                                        disabled={accept.isPending}
                                        aria-label={t("inbox.terms-tab.accept-aria", { title })}
                                        onClick={() => accept.mutate(term.termId)}
                                    >
                                        {accepting ? t("inbox.terms-tab.accepting") : t("inbox.terms-tab.accept")}
                                    </InboxButton>
                                </span>
                            </li>
                        );
                    })}
                </ul>
            )}
        </div>
    );
}
