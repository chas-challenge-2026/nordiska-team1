import type { Account } from "../services/accountsService";
import { formatCurrency } from "../utils/currency";
import { useTranslation } from "react-i18next";

interface AccountCardProps {
    account: Account;
}

export default function AccountCard({ account }: AccountCardProps) {
    const estimatedAnnualInterest = account.balance * ((account.interestRate * 100) / 100);
    const { t } = useTranslation();

    return (
        <article className="flex flex-col gap-4 rounded-lg border border-secondary/15 bg-white p-4 shadow-card sm:p-5">
            <div>
                <p className="font-semibold text-dark-navy">{account.accountName}</p>
                <p className="flex flex-wrap items-center gap-2 text-sm text-secondary">
                    <span>{account.accountType} {account.accountNumber}</span>
                    |
                    <span>{t("generic.interest")} {(account.interestRate * 100).toLocaleString("sv-SE")}%</span>
                </p>
            </div>

            <div>
                <p className="text-sm text-secondary">{t("generic.balance")}</p>
                <p className="font-montserrat-alternates text-2xl font-semibold tracking-tight text-dark-navy sm:text-3xl">
                    {formatCurrency(account.balance)}
                </p>
            </div>

            <div className="flex flex-wrap items-end justify-between gap-3 border-t border-secondary/15 pt-4">
                <div className="min-w-0">
                    <p className="text-sm text-secondary">{t("accounts-route.yearly-interest")}</p>
                    <p className="text-sm font-semibold text-dark-navy">
                        {formatCurrency(estimatedAnnualInterest)}
                    </p>
                    <p className="text-xs leading-snug text-secondary">
                        {t("accounts-route.yearly-interest-info")}
                    </p>
                </div>
                <button
                    type="button"
                    onClick={() => { }}
                    className="whitespace-nowrap rounded-md bg-primary-blue px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-nordiska-blue focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-light-blue-accent"
                >
                    {t("generic.tax-report")}
                </button>
            </div>
        </article>
    );
}
