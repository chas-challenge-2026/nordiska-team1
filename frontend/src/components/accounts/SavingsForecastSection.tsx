import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Area, AreaChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useAccountTransactionsSince } from "../../hooks/useTransactions";
import { formatCurrency } from "../../utils/currency";
import { buildInterestHistory, periodStart } from "../../utils/interestHistory";
import { LoadingState, ErrorState } from "../StatusMessage";

const PERIODS = [30, 90, 365] as const;
type Period = typeof PERIODS[number];
const MAX_PERIOD = PERIODS[PERIODS.length - 1];

type SavingsForecastSectionProps = {
    accountId: number;
    balance: number;
    interestRate: number;
    createdAt: string;
};

function formatShortDate(dateKey: string) {
    return new Date(`${dateKey}T00:00:00`).toLocaleDateString("sv-SE", { day: "numeric", month: "short" });
}

export default function SavingsForecastSection({accountId, balance, interestRate, createdAt}: SavingsForecastSectionProps) {
    const { t } = useTranslation();
    const [period, setPeriod] = useState<Period>(PERIODS[0]);

    // Hämta hela längsta perioden en gång, kortare perioder räknas ut lokalt
    const fromDate = useMemo(() => periodStart(MAX_PERIOD).toISOString(), []);
    const { data: transactions, isPending, isError } = useAccountTransactionsSince(accountId, fromDate);

    const history = useMemo(
        () => buildInterestHistory(balance, interestRate, createdAt, transactions ?? [], period),
        [balance, interestRate, createdAt, transactions, period],
    );

    return (
        <section aria-labelledby="forecast-heading" className="bg-white -mt-1">
            <h2 id="forecast-heading" className="text-base font-semibold text-dark-navy mb-1 sm:text-xl ml-1">{t("accounts-route.forecast-section.heading")}</h2>

            {/* PERIODVAL */}
            <div role="group" aria-label={t("accounts-route.forecast-section.period-label")} className="mb-2 flex gap-2">
                {PERIODS.map((days) => (
                    <button
                        key={days}
                        type="button"
                        aria-pressed={period === days}
                        onClick={() => setPeriod(days)}
                        className={`flex-1 py-1.5 text-sm font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-primary-blue sm:flex-none sm:px-5 ${
                            period === days
                                ? "border border-primary-blue bg-primary-blue text-white"
                                : "border border-primary-blue/40 text-primary-blue hover:bg-primary-blue-hover"
                        }`}
                    >
                        {t("accounts-route.forecast-section.days", { count: days })}
                    </button>
                ))}
            </div>

            <div className="border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:p-4">
                {isPending && <LoadingState title={t("accounts-route.forecast-section.loading")} />}
                {isError && <ErrorState title={t("accounts-route.forecast-section.error")} />}

                {!isPending && !isError && (
                    <>
                        <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.forecast-section.earned-interest", { count: period })}</p>
                        <p className="mt-1 text-lg font-bold text-dark-navy sm:mt-2 sm:text-xl">{formatCurrency(history.totalInterest)}</p>

                        <div className="mt-3 h-44 w-full" role="img" aria-label={t("accounts-route.forecast-section.chart-label", { count: period })}>
                            <ResponsiveContainer width="100%" height="100%">
                                <AreaChart data={history.points} margin={{ top: 5, right: 5, bottom: 0, left: 0 }}>
                                    <defs>
                                        <linearGradient id="forecast-fill" x1="0" y1="0" x2="0" y2="1">
                                            <stop offset="0%" stopColor="#0052BD" stopOpacity={0.3} />
                                            <stop offset="100%" stopColor="#0052BD" stopOpacity={0.02} />
                                        </linearGradient>
                                    </defs>
                                    <XAxis dataKey="date" tickFormatter={formatShortDate} tick={{ fontSize: 11, fill: "#515C6E" }} tickLine={false} axisLine={{ stroke: "#134663", strokeOpacity: 0.3 }} minTickGap={24} />
                                    <YAxis tickFormatter={(value: number) => value.toLocaleString("sv-SE", { maximumFractionDigits: 0 })} tick={{ fontSize: 11, fill: "#515C6E" }} tickLine={false} axisLine={false} width={44} />
                                    <Tooltip
                                        labelFormatter={(label) => formatShortDate(String(label))}
                                        formatter={(value) => [formatCurrency(Number(value)), t("accounts-route.forecast-section.tooltip")]}
                                        contentStyle={{ borderRadius: 0, borderColor: "#134663", fontSize: 12 }}
                                    />
                                    <Area type="monotone" dataKey="interest" stroke="#0052BD" strokeWidth={2} fill="url(#forecast-fill)" />
                                </AreaChart>
                            </ResponsiveContainer>
                        </div>

                        <p className="mt-2 text-[11px] leading-4 text-secondary sm:text-xs sm:leading-5">{t("accounts-route.forecast-section.info")}</p>
                    </>
                )}
            </div>
        </section>
    );
}
