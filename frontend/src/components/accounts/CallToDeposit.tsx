import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";

type CallToDepositProps = {
    accountId: number;
}

import ActionButton from "./ActionButton"

export default function CallToDeposit({accountId}: CallToDepositProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();

    return (
            <section aria-labelledby="deposit-heading" className="mt-4 sm:mt-8">
                <h2 id="deposit-heading" className="bg-dark-navy text-base font-medium text-white mb-1 sm:text-lg pl-1">        
                    Starta ditt sparande     
                </h2>

                <div className="border border-nordiska-blue/50 bg-light-gray/30 pb-4 flex flex-col items-center justify-center ">
                    <p className="mt-5 text-center text-sm font-semibold text-dark-navy/80 sm:text-lg">
                        NÅGONTING HÄR OM ATT GÖRA EN PROGNOS?
                    </p>
                    <p className="mt-1 px-2 text-center text-xs text-dark-navy/80 sm:px-10 sm:text-base xl:text-sm">
                        Kom igång redan idag!
                    </p>
                    <div className="mt-5">
                        <ActionButton
                            style="regular"
                            onClick= {() => {navigate(`/transfer?account=${accountId}`)}}
                            ariaLabel={t("accounts-route.savings-goal.create-savingsgoal")}
                            prefixIcon="/icons/deposit.svg"
                            title={t("accounts-route.savings-goal.deposit")}
                        />
                    </div>
                </div>
            </section>
    )
}