import { useMemo, useState } from "react";
import { Trans, useTranslation } from "react-i18next";
import { formatCurrency } from "../../utils/currency";
import { buildSavingsForecast } from "../../utils/savingsForecast";
import SliderField from "./SliderField";

type SavingsForecastSectionProps = {
    balance: number;
    interestRate: number;
    onDeposit: () => void;
};

export default function SavingsForecastSection({balance, interestRate, onDeposit}: SavingsForecastSectionProps) {
    const { t } = useTranslation();
    const [monthlyAmount, setMonthlyAmount] = useState(500);
    const [years, setYears] = useState(5);

    const forecast = useMemo(
        () => buildSavingsForecast(balance, interestRate, monthlyAmount, years),
        [balance, interestRate, monthlyAmount, years],
    );

    return (
        <section aria-labelledby="forecast-heading" className="bg-white -mt-1">
            <h2 id="forecast-heading" className="text-base font-semibold text-dark-navy mb-1 sm:text-xl ml-1">{t("accounts-route.forecast-section.heading")}</h2>

            <div className="border border-nordiska-blue/50 bg-light-gray/30">
                {/* RESULTAT */}
                <div aria-live="polite" className="border-b border-nordiska-blue/20 px-3 py-4 text-center sm:py-5">
                    <p className="text-xs text-secondary sm:text-sm">{t("accounts-route.forecast-section.earned-interest")}</p>
                    <p className="text-2xl font-bold tracking-tight text-green-700 sm:text-3xl">{formatCurrency(forecast.totalInterest)}</p>
                    <p className="mt-1 text-sm text-secondary sm:text-base">
                        <Trans
                            i18nKey="accounts-route.forecast-section.total"
                            values={{ amount: formatCurrency(forecast.endBalance) }}
                            components={{ amount: <span className="font-bold" /> }}
                        />
                    </p>
                </div>

                {/* REGLAGE */}
                <div className="flex flex-col gap-4 p-3 sm:p-4">
                    <SliderField
                        label={t("accounts-route.forecast-section.monthly-saving")}
                        value={monthlyAmount}
                        min={0}
                        max={10_000}
                        step={100}
                        onChange={setMonthlyAmount}
                        formatValue={(value) => `${value.toLocaleString("sv-SE", { maximumFractionDigits: 2 })} kr`}
                        unit="kr"
                        manualEntry="free"
                        inputMax={1_000_000}
                    />
                    <SliderField
                        label={t("accounts-route.forecast-section.saving-period")}
                        value={years}
                        min={1}
                        max={10}
                        step={1}
                        onChange={setYears}
                        formatValue={(value) => t("accounts-route.forecast-section.years", { count: value })}
                        unit={t("accounts-route.forecast-section.years-unit")}
                    />

                    {/* FASTA VÄRDEN */}
                    <dl className="flex flex-col gap-1 text-sm">
                        <div className="flex justify-between gap-3">
                            <dt className="text-secondary">{t("accounts-route.forecast-section.start-amount")}</dt>
                            <dd className="font-semibold text-dark-navy">{formatCurrency(balance)}</dd>
                        </div>
                        <div className="flex justify-between gap-3">
                            <dt className="text-secondary">{t("accounts-route.forecast-section.interest-rate")}</dt>
                            <dd className="font-semibold text-dark-navy">{(interestRate * 100).toLocaleString("sv-SE")} %</dd>
                        </div>
                    </dl>

                    <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                        <p className="text-[11px] leading-4 text-secondary sm:max-w-md sm:text-xs sm:leading-5">{t("accounts-route.forecast-section.info")}</p>
                        <button type="button" onClick={onDeposit} className="group inline-flex shrink-0 cursor-pointer items-center justify-center rounded-lg border bg-white px-2.5 py-1.5 text-xs font-semibold text-primary-blue transition hover:bg-primary-blue hover:text-white sm:text-sm max-sm:w-full">
                            <span aria-hidden="true" className="mr-1.5 block h-4 w-4 shrink-0 bg-primary-blue mask-[url('/icons/deposit.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-white sm:mr-2 sm:h-5 sm:w-5" />
                            {t("accounts-route.forecast-section.move-money")}
                        </button>
                    </div>
                </div>
            </div>
        </section>
    );
}
