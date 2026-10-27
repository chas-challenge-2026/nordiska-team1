import { useTranslation } from "react-i18next"
import { formatCurrency } from "../../utils/currency";

type InterestSectionProps = {
    accruedInterestYtd: number;
    estimatedYearEndInterest: number;
}

export default function InterestSection({accruedInterestYtd, estimatedYearEndInterest}: InterestSectionProps) {
    const {t} = useTranslation();

    return(
        <section aria-labelledby="interest-heading" className="bg-white -mt-1">
            <h2 id="interest-heading" className="bg-dark-navy text-base font-medium text-white mb-1 sm:text-lg pt-1 pl-1">{t("accounts-route.payback")}</h2>

            <div className="grid gap-2 sm:grid-cols-2 sm:gap-4">
                {/* UTDELNING */}
                <div className="border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:p-4">
                    <p className="text-xs uppercase text-nordiska-blue sm:text-sm"> {t("accounts-route.interest-section.accured-interest")} </p>
                    <p className={`mt-1 text-lg font-bold text-dark-navy sm:mt-2 sm:text-2xl ${accruedInterestYtd > 0 ? "text-green-700" : ""}`}>{formatCurrency(accruedInterestYtd)}</p>
                    <p className="mt-1 text-[11px] leading-4 text-secondary sm:mt-2 sm:text-xs sm:leading-5">  {t("accounts-route.interest-section.accured-interest-info")} </p>
                </div>

                {/* BERÄKNAD RÄNTA */}
                <div className="border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:p-4">
                    <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.interest-section.yearly-interest")}</p>
                    <p className="mt-1 text-lg font-bold text-dark-navy sm:mt-2 sm:text-2xl">{formatCurrency(estimatedYearEndInterest)}</p>
                    <p className="mt-1 text-[11px] leading-4 text-secondary sm:mt-2 sm:text-xs sm:leading-5">{t("accounts-route.interest-section.yearly-interest-info")}</p>
                </div>
            </div>
        </section>
    )
}