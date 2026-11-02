import { useTranslation } from "react-i18next";
import { formatCurrencySmallSek } from "../../utils/currency";

type ForecastResultProps = {
    totalInterest: number;
    endBalance: number;
};

/** Resultatet av en sparprognos: intjänad ränta stort och grönt, totalen under */
export default function ForecastResult({ totalInterest, endBalance }: ForecastResultProps) {
    const { t } = useTranslation();

    return (
        <div aria-live="polite" className="border-b border-nordiska-blue/20 px-3 py-4 text-center sm:py-5">
            <div>
                <div className="text-sm">
                    <p>{t("accounts-route.forecast-section.total")}</p>
                    <span className="text-2xl font-semibold ">{formatCurrencySmallSek(endBalance, true)}</span>
                    <p>{t("accounts-route.forecast-section.earned-interest")}</p>
                    <span className={`text-2xl font-bold ${totalInterest ? "text-green-700" : ""}`}>{formatCurrencySmallSek(totalInterest, true)}</span>
                </div>
            </div>
        </div>
    );
}
