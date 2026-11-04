import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { formatCurrencySmallSek } from "../../utils/currency";
import { buildSavingsForecast } from "../../utils/savingsForecast";
import ForecastResult from "./ForecastResult";
import SliderField from "./SliderField";
import ActionButton from "./ActionButton";

type SavingsForecastSectionProps = {
    balance: number;
    interestRate: number;
    onDeposit: (monthlyAmount: number) => void;
    zeroAccount?: boolean;
};

export default function SavingsForecastSection({balance, interestRate, onDeposit, zeroAccount = false}: SavingsForecastSectionProps) {
    const intitialYears = zeroAccount ? 0 : 1;
    const { t } = useTranslation();
    const [monthlyAmount, setMonthlyAmount] = useState(0);
    const [years, setYears] = useState(intitialYears);

    const forecast = useMemo(
        () => buildSavingsForecast(balance, interestRate, monthlyAmount, years),
        [balance, interestRate, monthlyAmount, years],
    );

    return (
            <div>
                {/* RESULTAT */}
                <ForecastResult totalInterest={forecast.totalInterest} endBalance={forecast.endBalance}/>

                {/* REGLAGE */}
                <div className="flex flex-col gap-4 p-3 sm:p-4">
                    <SliderField
                        label={t("accounts-route.forecast-section.monthly-saving")}
                        value={monthlyAmount}
                        min={0}
                        max={10_000}
                        step={100}
                        onChange={setMonthlyAmount}
                        formatValue={(value) => `${value.toLocaleString("sv-SE", { maximumFractionDigits: 2 })} sek`}
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
                        {!zeroAccount && 
                            <div className="flex justify-between gap-3">
                                <dt className="text-secondary">{t("accounts-route.forecast-section.start-amount")}</dt>
                                <dd className="font-semibold text-dark-navy">{formatCurrencySmallSek(balance)}</dd>
                            </div>
                        }

                        <div className="flex justify-between gap-3">
                            <dt className="text-secondary">{t("accounts-route.forecast-section.interest-rate")}</dt>
                            <dd className="font-semibold text-dark-navy">{(interestRate * 100).toLocaleString("sv-SE")} %</dd>
                        </div>
                    </dl>

                    <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
                        <p className="text-[11px] w-[50%] leading-4 text-secondary sm:max-w-md sm:text-xs sm:leading-5">
                            {t("accounts-route.forecast-section.info")}
                        </p>

                        <ActionButton
                            style="regular"
                            onClick={() => onDeposit(monthlyAmount)}
                            ariaLabel={t("accounts-route.savings-goal.create-savingsgoal")}
                            prefixIcon="/icons/deposit.svg"
                            title={t("accounts-route.savings-goal.deposit")}
                        />
                    </div>
                </div>
            </div>
    );
}
