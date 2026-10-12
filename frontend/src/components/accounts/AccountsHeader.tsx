import { useTranslation } from "react-i18next"
import { formatCurrency } from "../../utils/currency";
import TaxReportBtn from "../reports/TaxReportButton";

type AccountsHeaderProps = {
    totalBalance: number;
    selectedAccountId: number | undefined;
    onCreateAccount: () => void;
}

export default function AccountsHeader({totalBalance, selectedAccountId, onCreateAccount}: AccountsHeaderProps) {
    const {t} = useTranslation();

    return(
        <header className="mb-5">
                <div className="mb-3 grid gap-5 items-end lg:border-b-2 lg:border-nordiska-orange lg:pb-2 lg:grid-cols-[minmax(220px,280px)_1fr] xl:grid-cols-[280px_1fr_240px]">
                    {/* titel: Mina Konton */}
                    <h1 className="w-full border-b-2 border-nordiska-orange pb-2 text-2xl font-bold tracking-tight text-dark-navy sm:text-3xl lg:w-auto lg:border-0 lg:pb-0">{t("accounts-route.accounts-header.my-accounts")}</h1>

                    {/* saldo och knappar */}
                    <div className="flex flex-col gap-5 lg:flex-row lg:justify-between lg:items-end lg:col-span-1 xl:col-span-2">

                        {/* TOTALT SALDO */}
                        <div className="flex items-end justify-start ml-1">
                            <p className="mb-0.5 mr-3 text-sm font-medium uppercase tracking-wide text-nordiska-blue">{t("accounts-route.accounts-header.total-balance")}</p>
                            <p className="text-2xl font-bold tracking-tight text-dark-navy">{formatCurrency(totalBalance)}</p>
                        </div>

                        {/* ACTION BUTTONS */}
                        <div className="flex w-full flex-row gap-2 lg:w-auto">
                            <button
                                onClick={onCreateAccount}
                                aria-label={t("accounts-route.aria-label.accounts-header.new-account")}
                                className="inline-flex min-w-0 flex-1 items-center justify-center rounded-lg px-2.5 py-1.5 text-xs font-semibold text-white transition cursor-pointer bg-primary-blue hover:bg-nordiska-blue sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm"
                            >
                                <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/plus.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5" />
                                {t("accounts-route.accounts-header.new-account")}
                            </button>

                        <TaxReportBtn key={selectedAccountId} accountId={selectedAccountId} />
                        </div>
                    </div>
                </div>
            </header>
    )}