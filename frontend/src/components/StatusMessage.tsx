import { useTranslation } from "react-i18next";
import InboxButton from "./inbox/InboxButton";

type StatusMessageProps = {
    title?: string;
    message?: string;
    onRetry?: () => void;
};

export function LoadingState({ title, message}: StatusMessageProps) {
    return (
        <div className="h-full flex flex-col items-center justify-center">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 animate-[spin_1.5s_steps(8)_infinite] bg-light-blue-accent mask-[url('/icons/throbber.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}

export function ErrorState({ title, message, onRetry}: StatusMessageProps) {
    const { t } = useTranslation();
    return (
        <div className="h-full flex flex-col items-center justify-center">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-red-700 mask-[url('/icons/frown.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
            {onRetry && <InboxButton variant="link" onClick={onRetry}>{t("generic.try-again")}</InboxButton>}
        </div>
    );
}

export function EmptyInboxState({ title, message}: StatusMessageProps) {
    return (
        <div className="h-full flex flex-col items-center justify-center">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-light-blue-accent mask-[url('/icons/inbox.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            {title && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {title} </p>)}
            {message && (<p role="alert" className="font-semibold mt-1 text-dark-navy/80"> {message} </p>)}
        </div>
    );
}