import { useTranslation } from "react-i18next";
import InboxButton from "./inbox/InboxButton";
import { useNavigate } from "react-router";

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

export function NoFavorites() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex flex-col items-center justify-center mt-20">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-light-blue-accent mask-[url('/icons/star.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold md:text-xl mt-1 text-dark-navy/80">{t("overview-route.no-favs")}</p>
            <p className="max-w-2xl px-4 text-center text-[0.8em] md:text-base mt-1 text-dark-navy/80">{t("overview-route.no-favs-description")}</p>
            <p className="mt-4 text-center text-sm md:text-base font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/accounts")} className="text-primary-blue cursor-pointer hover:text-primary-hover">{t("page-navigation.accounts")}</span></p>
        </div>
    );
}

type NoAccountsProp = {onCreateAccount: () => void;}

export function NoAccounts({onCreateAccount}: NoAccountsProp) {
    const {t} = useTranslation();

    return (
        <div className="flex flex-col items-center justify-center mt-20">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-dark-navy mask-[url('/icons/no-accounts.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold md:text-xl mt-1 text-dark-navy/80">{t("overview-route.no-accounts")}</p>
            <p className=" max-w-2xl px-4 text-center text-[0.8em] md:text-base mt-1 text-dark-navy/80">{t("overview-route.no-accounts-description")}</p>
            <button
                type="button"
                onClick={onCreateAccount}
                aria-label={t("accounts-route.aria-label.new-account")}
                className="inline-flex min-w-0 items-center justify-center rounded-lg bg-primary-blue px-2.5 py-1.5 text-xs font-semibold text-white transition hover:bg-primary-hover cursor-pointer sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm mt-4 md:mt-6"
            >
                <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/plus.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5"/>
                {t("accounts-route.accounts-header.new-account")}
            </button>
        </div>
    );
}

export function NoTransactions() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-light-blue-accent mask-[url('/icons/transactions.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold mt-1 text-dark-navy/80">{t("overview-route.transactions-card.no-transactions")}</p>
            <p className="px-4 text-center text-[0.8em] mt-1 text-dark-navy/80">{t("overview-route.transactions-card.no-transactions-description")}</p>
            <p className="mt-4 text-center text-sm font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/transfer")} className="text-primary-blue cursor-pointer hover:text-primary-hover">{t("page-navigation.transfers")}</span></p>
        </div>
    );
}

export function NoPlannedState() {
    const {t} = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-light-blue-accent mask-[url('/icons/transfers.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold mt-1 text-dark-navy/80"> {t("no-planned-state.title")} </p>
            <p className="px-4 text-center text-[0.8em] mt-1 text-dark-navy/80"> {t("no-planned-state.description")}</p>
            <p className="mt-4 text-center text-sm font-semibold text-dark-navy/80 md:mt-6">{t("no-planned-state.action")} <span onClick={() => navigate("/transfer")} className="text-primary-blue cursor-pointer hover:text-primary-hover">{t("page-navigation.transfers")}</span></p>
        </div>
    );
}