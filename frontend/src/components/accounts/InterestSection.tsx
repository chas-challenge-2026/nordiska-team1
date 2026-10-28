import { useTranslation } from "react-i18next"
import { formatCurrencySmallSek } from "../../utils/currency";

type InterestSectionProps = {
    accruedInterestYtd: number;
    estimatedYearEndInterest: number;
}

export default function InterestSection({accruedInterestYtd, estimatedYearEndInterest}: InterestSectionProps) {
    const {t} = useTranslation();

    return(
        <section aria-labelledby="interest-heading" className="bg-white -mt-1">
            <h2 id="interest-heading" className="bg-dark-navy px-2 pt-1 text-base font-medium text-white sm:text-lg"> {t("accounts-route.payback")} </h2>

            <div className="grid grid-cols-2 border border-dark-navy/50 bg-light-gray/30 sm:gap-0">

                {/* UTDELNING */}
                <div className="relative min-w-0 p-2.5 sm:p-4 sm:after:absolute sm:after:right-0 sm:after:top-5 sm:after:bottom-5 sm:after:w-[2px] sm:after:bg-light-blue-accent">
                    <p className="break-words text-xs uppercase text-dark-navy sm:text-sm">
                        {t("accounts-route.interest-section.accured-interest")}
                    </p>
                    <p className={`mt-1 break-words text-base font-bold sm:mt-2 sm:text-2xl ${accruedInterestYtd > 0 ? "text-green-700" : "text-dark-navy"}`}>
                        {formatCurrencySmallSek(accruedInterestYtd)}
                    </p>
                    <p className="mt-1 break-words text-[11px] leading-4 text-secondary sm:mt-2 sm:text-xs sm:leading-5">
                        {t("accounts-route.interest-section.accured-interest-info")}
                    </p>
                </div>

                {/* BERÄKNAD RÄNTA */}
                <div className="min-w-0 p-2.5 sm:p-4">
                    <p className="break-words text-xs uppercase text-dark-navy sm:text-sm">
                        {t("accounts-route.interest-section.yearly-interest")}
                    </p>
                    <p className="mt-1 break-words text-base font-bold text-dark-navy sm:mt-2 sm:text-2xl">
                        {formatCurrencySmallSek(estimatedYearEndInterest)}
                    </p>
                    <p className="mt-1 break-words text-[11px] leading-4 text-secondary sm:mt-2 sm:text-xs sm:leading-5">
                        {t("accounts-route.interest-section.yearly-interest-info")}
                    </p>
                </div>
            </div>
        </section>
    );
}