import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { useAccountTransactionsSince } from "../../hooks/useTransactions";
import { formatCurrency } from "../../utils/currency";
import { buildInterestHistory, periodStart } from "../../utils/interestHistory";
import { LoadingState, ErrorState } from "../StatusMessage";

const MIN_DAYS = 1;
const MAX_DAYS = 365;

type SavingsForecastSectionProps = {
    accountId: number;
    balance: number;
    interestRate: number;
    createdAt: string;
};

// Små belopp behöver decimaler på Y-axeln, annars blir skalstegen t.ex. "0, 0, 1, 1"
function formatAxisAmount(value: number, maxValue: number) {
    return value.toLocaleString("sv-SE", { maximumFractionDigits: maxValue < 10 ? 2 : 0 });
}

function formatShortDate(dateKey: string) {
    return new Date(`${dateKey}T00:00:00`).toLocaleDateString("sv-SE", { day: "numeric", month: "short" });
}

export default function SavingsForecastSection({accountId, balance, interestRate, createdAt}: SavingsForecastSectionProps) {
    const { t } = useTranslation();
    const [period, setPeriod] = useState(30);

    // Hämta hela längsta perioden en gång, kortare perioder räknas ut lokalt
    const fromDate = useMemo(() => periodStart(MAX_DAYS).toISOString(), []);
    const { data: transactions, isPending, isError } = useAccountTransactionsSince(accountId, fromDate);

    const history = useMemo(
        () => buildInterestHistory(balance, interestRate, createdAt, transactions ?? [], period),
        [balance, interestRate, createdAt, transactions, period],
    );

    return (
        <section aria-labelledby="forecast-heading" className="bg-white -mt-1">
            <h2 id="forecast-heading" className="text-base font-semibold text-dark-navy mb-1 sm:text-xl ml-1">{t("accounts-route.forecast-section.heading")}</h2>

            {/* PERIODVAL */}
            <div className="mb-2 px-1">
                <div className="flex items-baseline justify-between">
                    <label htmlFor="forecast-days" className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.forecast-section.slider-label")}</label>
                    <span aria-hidden="true" className="text-base font-bold text-primary-blue sm:text-lg">{t("accounts-route.forecast-section.days", { count: period })}</span>
                </div>
                <input
                    id="forecast-days"
                    type="range"
                    min={MIN_DAYS}
                    max={MAX_DAYS}
                    step={1}
                    value={period}
                    onChange={(e) => setPeriod(Number(e.target.value))}
                    aria-valuetext={t("accounts-route.forecast-section.days", { count: period })}
                    className="mt-1 w-full cursor-pointer accent-primary-blue"
                />
                <div aria-hidden="true" className="flex justify-between text-[11px] text-secondary sm:text-xs">
                    <span>{t("accounts-route.forecast-section.days", { count: MIN_DAYS })}</span>
                    <span>{t("accounts-route.forecast-section.days", { count: MAX_DAYS })}</span>
                </div>
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
                                <AreaChart data={history.points} margin={{ top: 10, right: 12, bottom: 0, left: 0 }}>
                                    <defs>
                                        <linearGradient id="forecast-fill" x1="0" y1="0" x2="0" y2="1">
                                            <stop offset="0%" stopColor="#0052BD" stopOpacity={0.25} />
                                            <stop offset="100%" stopColor="#0052BD" stopOpacity={0.02} />
                                        </linearGradient>
                                    </defs>
                                    <CartesianGrid vertical={false} stroke="#134663" strokeOpacity={0.12} strokeDasharray="3 3" />
                                    <XAxis dataKey="date" tickFormatter={formatShortDate} tick={{ fontSize: 11, fill: "#515C6E" }} tickLine={false} axisLine={{ stroke: "#134663", strokeOpacity: 0.3 }} minTickGap={32} tickMargin={6} />
                                    <YAxis domain={[0, "auto"]} tickCount={4} tickFormatter={(value: number) => formatAxisAmount(value, history.totalInterest)} tick={{ fontSize: 11, fill: "#515C6E" }} tickLine={false} axisLine={false} width={48} />
                                    <Tooltip
                                        labelFormatter={(label) => formatShortDate(String(label))}
                                        formatter={(value) => [formatCurrency(Number(value)), t("accounts-route.forecast-section.tooltip")]}
                                        contentStyle={{ borderRadius: 0, borderColor: "#134663", fontSize: 12 }}
                                        cursor={{ stroke: "#0052BD", strokeOpacity: 0.3 }}
                                    />
                                    <Area type="monotone" dataKey="interest" stroke="#0052BD" strokeWidth={2} fill="url(#forecast-fill)" dot={false} activeDot={{ r: 4, fill: "#0052BD", stroke: "#fff", strokeWidth: 2 }} isAnimationActive={false} />
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
