import { useTranslation } from "react-i18next";
import { formatCurrency } from "../../utils/currency";
import TaxReportBtn from "../reports/TaxReportButton";

type TotalBalanceAndBtnsProps = {
    totalBalance: number;
    selectedAccountId: number | undefined;
    onCreateAccount: () => void;
}

export default function TotalBalanceAndBtns({totalBalance, selectedAccountId, onCreateAccount}: TotalBalanceAndBtnsProps) {
    const {t} = useTranslation();
    
    return (
    <div className="overflow-hidden border border-gray-200 bg-white pb-4 max-[991px]:bg-nordiska-bg max-[991px]:border-0 max-[991px]:pb-0 max-[991px]:-mb-4">
        <div className="flex flex-col max-[1199px]:flex-row max-[1199px]:items-center max-[1199px]:justify-between">

            {/* --------------- VISAS BARA I 3 GRID --------------- */}
            <div className="max-[1200px]:hidden">
                <div className="flex min-h-22 items-center justify-between border-b border-primary-blue/40 bg-dark-navy px-4 py-3">
                    <div className="min-w-0 text-white">
                        <p className="truncate text-xs uppercase text-white/70">
                            {t("accounts-route.info")}
                        </p>
                        <p className="mt-1 truncate text-md font-semibold">
                            {t("accounts-route.overview")}
                        </p>
                    </div>
                </div>
            </div>

            {/* --------------- VISAS BARA PÅ MOBIL --------------- */}
            <div className="flex w-full justify-between items-end mb-7 min-[500px]:hidden">
                <div>
                    <p className="truncate text-sm text-dark-navy">{t("accounts-route.accounts-header.total-balance")}</p>
                    <p className="truncate text-md font-semibold">{formatCurrency(totalBalance)}</p>
                </div>
                <div className="flex gap-1 mb-1">
                    <button
                        type="button"
                        onClick={onCreateAccount}
                        aria-label={t("accounts-route.aria-label.generate-full-report")}
                        className="inline-flex min-w-0 flex-1 items-center justify-center rounded-lg py-1.5 text-xs font-semibold text-white transition cursor-pointer bg-primary-blue sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm disabled:opacity-50"
                    >
                        {t("accounts-route.accounts-header.new-account")}
                    </button>
                    <TaxReportBtn key={selectedAccountId} accountId={selectedAccountId} />
                </div>
            </div>
            
            {/* --------------- VISAS BARA PÅ DESKTOP --------------- */}
            <div className="flex min-h-22 w-full items-center justify-between border-b border-primary-blue/40 px-3 py-3 text-left max-[1200px]:w-fit max-[1200px]:border-0 max-[499px]:hidden">
                <div className="min-w-0 max-[1200px]:flex max-[1200px]:items-end">
                    <p className="truncate text-sm text-dark-navy">{t("accounts-route.accounts-header.total-balance")}</p>
                    <p className="mt-1 truncate text-md font-semibold text-dark-navy max-[1200px]:ml-2">{formatCurrency(totalBalance)}</p>
                </div>
            </div>

            {/* KNAPPAR */}
            <div className="flex flex-col gap-2 px-3 mt-4 max-[1199px]:mt-0 max-[1199px]:flex-row max-[1199px]:max-h-[40px] max-[499px]:hidden">
                <button
                    onClick={onCreateAccount}
                    aria-label={t("accounts-route.aria-label.new-account")}
                    className="inline-flex min-w-0 flex-1 items-center justify-center rounded-lg px-2.5 py-1.5 text-xs font-semibold text-white transition cursor-pointer bg-primary-blue hover:bg-primary-hover sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm"
                >
                    <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/plus.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5" />
                    {t("accounts-route.accounts-header.new-account")}
                </button>
                <TaxReportBtn key={selectedAccountId} accountId={selectedAccountId} />
            </div>
        </div>
    </div>
    )
}