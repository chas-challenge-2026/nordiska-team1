import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useAccountTransactionsSince } from "../../hooks/useTransactions";
import { formatCurrency } from "../../utils/currency";
import { buildInterestForecast, calculateEarnedInterest, daysForMonths, periodStart } from "../../utils/interestHistory";
import { LoadingState, ErrorState } from "../StatusMessage";
import InputField from "../forms/InputField";

const PERIODS = [1, 3, 6, 12] as const;
type Period = typeof PERIODS[number];
const MAX_DEPOSIT = 10_000_000;
const LABEL = "text-xs uppercase text-nordiska-blue sm:text-sm";
const AMOUNT = "mt-0.5 text-lg font-bold text-dark-navy sm:mt-1 sm:text-xl";

type SavingsForecastSectionProps = {
    accountId: number;
    balance: number;
    interestRate: number;
    createdAt: string;
    onDeposit: () => void;
};

export default function SavingsForecastSection({accountId, balance, interestRate, createdAt, onDeposit}: SavingsForecastSectionProps) {
    const { t } = useTranslation();
    const [months, setMonths] = useState<Period>(1);
    const [depositInput, setDepositInput] = useState("");
    const [monthly, setMonthly] = useState(false);

    // Tomt, 0, negativt eller ogiltigt belopp räknas som ingen insättning
    const parsedDeposit = Number(depositInput.replace(",", "."));
    const depositAmount = Number.isFinite(parsedDeposit) && parsedDeposit > 0 ? Math.min(parsedDeposit, MAX_DEPOSIT) : 0;
    const hasDeposit = depositAmount > 0;

    // Hämta hela längsta perioden bakåt en gång, kortare perioder räknas ut lokalt
    const fromDate = useMemo(() => periodStart(daysForMonths(-PERIODS[PERIODS.length - 1])).toISOString(), []);
    const { data: transactions, isPending, isError } = useAccountTransactionsSince(accountId, fromDate);

    const earned = useMemo(
        () => calculateEarnedInterest(balance, interestRate, createdAt, transactions ?? [], daysForMonths(-months)),
        [balance, interestRate, createdAt, transactions, months],
    );

    const forecast = useMemo(
        () => buildInterestForecast(balance, interestRate, daysForMonths(months), depositAmount, monthly),
        [balance, interestRate, months, depositAmount, monthly],
    );

    return (
        <section aria-labelledby="forecast-heading" className="bg-white -mt-1">
            <h2 id="forecast-heading" className="text-base font-semibold text-dark-navy mb-1 sm:text-xl ml-1">{t("accounts-route.forecast-section.heading")}</h2>

            {/* PERIODVAL */}
            <div role="group" aria-label={t("accounts-route.forecast-section.period-label")} className="mb-2 flex gap-2">
                {PERIODS.map((period) => (
                    <button
                        key={period}
                        type="button"
                        aria-pressed={months === period}
                        onClick={() => setMonths(period)}
                        className={`flex-1 py-1.5 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-primary-blue sm:flex-none sm:px-5 ${
                            months === period
                                ? "border border-primary-blue bg-primary-blue text-white"
                                : "border border-primary-blue/40 text-primary-blue hover:bg-primary-blue-hover"
                        }`}
                    >
                        {t("accounts-route.forecast-section.months-short", { count: period })}
                    </button>
                ))}
            </div>

            <div className="border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:p-4">
                {/* INTJÄNAT OCH KOMMANDE */}
                <div className="grid gap-3 sm:grid-cols-2 sm:gap-4">
                    <div>
                        <p className={LABEL}>{t("accounts-route.forecast-section.earned", { count: months })}</p>
                        {isPending && <LoadingState title={t("accounts-route.forecast-section.loading")} />}
                        {isError && <ErrorState title={t("accounts-route.forecast-section.error")} />}
                        {!isPending && !isError && <p className={AMOUNT}>{formatCurrency(earned)}</p>}
                    </div>
                    <div>
                        <p className={LABEL}>{t("accounts-route.forecast-section.upcoming", { count: months })}</p>
                        <p className={AMOUNT}>{formatCurrency(forecast.withoutInterest)}</p>
                    </div>
                </div>

                {/* TESTA EN INSÄTTNING */}
                <div className="mt-4 border-t border-nordiska-blue/20 pt-3">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-end sm:gap-4">
                        <div className="sm:w-56">
                            <InputField
                                name="forecast-deposit"
                                type="number"
                                label={t("accounts-route.forecast-section.deposit-label")}
                                placeholder="500"
                                value={depositInput}
                                onChange={setDepositInput}
                                suffix="kr"
                            />
                        </div>
                        <label className="flex cursor-pointer items-center gap-2 text-sm font-medium text-dark-navy sm:pb-2.5">
                            <input
                                type="checkbox"
                                checked={monthly}
                                onChange={(e) => setMonthly(e.target.checked)}
                                className="h-4 w-4 cursor-pointer accent-primary-blue"
                            />
                            {t("accounts-route.forecast-section.monthly")}
                        </label>
                    </div>

                    {hasDeposit && (
                        <>
                            <div className="mt-3 grid gap-3 sm:grid-cols-3 sm:gap-4">
                                <div>
                                    <p className={LABEL}>{t("accounts-route.forecast-section.with-deposit")}</p>
                                    <p className={AMOUNT}>{formatCurrency(forecast.withInterest)}</p>
                                    <p className="text-sm font-semibold text-success">{formatCurrency(forecast.difference, { signed: true })}</p>
                                </div>
                                <div>
                                    <p className={LABEL}>{t("accounts-route.forecast-section.total-deposited")}</p>
                                    <p className={AMOUNT}>{formatCurrency(forecast.totalDeposited)}</p>
                                    <p className="text-sm text-secondary">{t("accounts-route.forecast-section.deposit-count", { count: forecast.depositCount })}</p>
                                </div>
                                <div>
                                    <p className={LABEL}>{t("accounts-route.forecast-section.balance-after", { count: months })}</p>
                                    <p className={AMOUNT}>{formatCurrency(forecast.endBalanceWith)}</p>
                                </div>
                            </div>

                            <div className="mt-3 flex justify-end">
                                <button type="button" onClick={onDeposit} className="group inline-flex cursor-pointer items-center justify-center rounded-lg border bg-white px-2.5 py-1.5 text-xs font-semibold text-primary-blue transition hover:bg-primary-blue hover:text-white sm:text-sm max-sm:w-full">
                                    <span aria-hidden="true" className="mr-1.5 block h-4 w-4 shrink-0 bg-primary-blue mask-[url('/icons/deposit.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-white sm:mr-2 sm:h-5 sm:w-5" />
                                    {t("accounts-route.forecast-section.move-money")}
                                </button>
                            </div>
                        </>
                    )}
                </div>

                <p className="mt-3 text-[11px] leading-4 text-secondary sm:text-xs sm:leading-5">{t("accounts-route.forecast-section.info")}</p>
            </div>
        </section>
    );
}
