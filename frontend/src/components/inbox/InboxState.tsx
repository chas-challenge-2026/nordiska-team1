import { useTranslation } from "react-i18next";
import InboxButton from "./InboxButton";

export function InboxLoading() {
    const { t } = useTranslation();
    return <p role="status" className="px-4 py-3 text-secondary">{t("generic.loading")}</p>;
}

export function InboxLoadError({ onRetry }: { onRetry: () => void }) {
    const { t } = useTranslation();
    return (
        <div className="flex items-center justify-between px-4 py-3">
            <span role="alert" className="text-error">{t("generic.error-load")}</span>
            <InboxButton variant="link" onClick={onRetry}>{t("generic.try-again")}</InboxButton>
        </div>
    );
}
