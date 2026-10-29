import { Trans, useTranslation } from "react-i18next";
import { formatCurrency } from "../../utils/currency";

type ForecastResultProps = {
    totalInterest: number;
    endBalance: number;
};

/** Resultatet av en sparprognos: intjänad ränta stort och grönt, totalen under */
export default function ForecastResult({ totalInterest, endBalance }: ForecastResultProps) {
    const { t } = useTranslation();

    return (
        <div aria-live="polite" className="border-b border-nordiska-blue/20 px-3 py-4 text-center sm:py-5">
            <p className="text-xs text-secondary sm:text-sm">{t("accounts-route.forecast-section.earned-interest")}</p>
            <p className="text-2xl font-bold tracking-tight text-green-700 sm:text-3xl">{formatCurrency(totalInterest)}</p>
            <p className="mt-1 text-sm text-secondary sm:text-base">
                <Trans
                    i18nKey="accounts-route.forecast-section.total"
                    values={{ amount: formatCurrency(endBalance) }}
                    components={{ amount: <span className="font-bold" /> }}
                />
            </p>
        </div>
    );
}
