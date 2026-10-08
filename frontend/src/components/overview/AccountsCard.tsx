import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { formatCurrency } from "../../utils/currency";
import { useGetAccounts } from "../../hooks/useAccounts";
import { LoadingState, ErrorState } from "../StatusMessage";
import { useNavigate } from "react-router";

const MAX_ROWS_UNTIL_FAVORITE_KEY_ON_ACCOUNT_EXISTS = 4;

export default function AccountsCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();

    const { data: accounts, isPending, isError } = useGetAccounts("active");
    const totalBalance = accounts?.reduce((sum, a) => sum + a.balance, 0) ?? 0;
    return (
    <OverviewCard>
        <div className="flex h-full flex-col">
            {/* CARD HEADER */}
            <div className="h-22 rounded-t-3xl bg-dark-navy pl-2 pt-1.5 text-white sm:pl-3">
                <div className="flex flex-col items-start p-3 sm:p-4">
                    <h2 className="text-xl font-semibold tracking-wide sm:text-2xl"> {t("overview-route.account-card.title")} </h2>
                    <p className="text-[0.65em] uppercase text-white/80 sm:text-[0.70em]"> {t("overview-route.account-card.description")} </p>
               </div>
            </div>

            {/* USER FEEDBACK */}
            {isPending && ( <LoadingState title={t("overview-route.account-card.pending")} />)}
            {isError && ( <ErrorState title={t("overview-route.account-card.error")} />)}

            {/* CONTENT */}
            {!isPending && !isError && (
                <section className="flex flex-1 flex-col p-2 sm:p-3">
                <div className="flex flex-1 flex-col rounded-b-xl overflow-hidden">

                    {/* TOTALT SALDO */}
                    <div className="flex h-15 flex-1 items-center justify-between border-b-2 border-nordiska-blue/50 bg-primary-blue/10 px-3 py-2 sm:px-5">
                        <p className="text-sm font-semibold sm:text-xl"> {t("overview-route.account-card.total-balance")}</p>
                        <p className="text-sm font-semibold sm:text-xl"> {formatCurrency(totalBalance)}</p>
                    </div>

                    {accounts?.slice(0, MAX_ROWS_UNTIL_FAVORITE_KEY_ON_ACCOUNT_EXISTS).map((account) => (
                        <button 
                            key={account.id}
                            title={`${t("overview-route.account-card.hover-title")} ${account.accountNumber}`}
                            type="button"
                            onClick={() => navigate(`/accounts?account=${account.id}`)}
                            className="flex flex-1 flex-col justify-center border-b-2 border-nordiska-orange px-3 py-2 odd:bg-light-gray/60 even:bg-white last:border-0 sm:px-5 cursor-pointer hover:bg-primary-blue/10">

                            {/* KONTONAMN OCH SALDO */}
                            <p className="flex items-baseline justify-between gap-2 text-sm sm:text-base">
                                <span className="min-w-0 wrap-break-word font-semibold">
                                    {account.accountName
                                        ? account.accountName
                                        : t(`accounts-route.account-type-${account.accountType}`)}
                                </span>
                                <span className="shrink-0 whitespace-nowrap font-medium"> {formatCurrency(account.balance)} </span>
                            </p>
                            {/* KONTO NR OCH RÄNTA */}
                            <p className="flex flex-wrap justify-between gap-x-2 gap-y-1 text-[9px] uppercase text-dark-navy/60 sm:text-xs">
                                <span className="min-w-0 wrap-break-word"> {account.accountNumber} </span>
                                <span className="shrink-0 whitespace-nowrap">{t(`accounts-route.account-type-${account.accountType}`)} {(account.interestRate * 100).toLocaleString("sv-SE")}<span className="font-normal"> %</span> </span>
                            </p>
                        </button>
                    ))}
                </div>
                </section>
            )}
        </div>
    </OverviewCard>
    );
}