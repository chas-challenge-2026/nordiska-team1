import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import OverviewCard from "./OverviewCard";
import { formatCurrencySmallSek } from "../../utils/currency";
import type { Account } from "../../services/accountsService";
import { useGetAllSavingsGoals } from "../../hooks/useAccounts";
import progressColor from "../../utils/savinggoals";

export type AccountCardProps = {
    account: Account;
}

export default function AccountCard({account}:AccountCardProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const accountName = account.accountName ? account.accountName : t(`accounts-route.account-type-${account.accountType}`);
    const { data: goals } = useGetAllSavingsGoals();
    const savingGoal = goals?.goals.find((goal) => goal.accountId === account.id && goal.status === "active");
    const savingGoalPercentage = savingGoal?.targetAmount ? (account.balance / savingGoal.targetAmount) * 100 : 0;
    const displayedPercentage = savingGoalPercentage === 0
        ? "0"
        : savingGoalPercentage < 1
            ? savingGoalPercentage.toLocaleString("sv-SE", {
                minimumFractionDigits: 1,
                maximumFractionDigits: 1,
            })
            : Math.round(savingGoalPercentage).toString();

    const barColor = progressColor(savingGoalPercentage);

    return (
        <OverviewCard>
            <div className="relative flex h-full flex-col">

                {/* CARD HEADER */}
                <div className="h-16 shrink-0 bg-dark-navy text-white p-3 flex items-center ">
                        <div className="w-full flex flex-wrap justify-between text-sm text-white/90">
                            <p>{account.accountNumber}</p>
                            <p className="font-medium">{t(`accounts-route.account-type-${account.accountType}`)} <span>{(account.interestRate * 100).toLocaleString("sv-SE")}<span className="font-extralight"> %</span></span></p>
                        </div>
                </div>

                <section className={`flex min-h-0 flex-1 flex-col ${savingGoal ? "justify-between" : "gap-3"} p-3 sm:overflow-y-auto`}>

                    {/* KONTONAMN */}
                    <div>
                        <span className="text-xs uppercase text-primary-hover">{t("accounts-route.account-name")}</span>
                        <div className="flex">
                            <h2 title={accountName} className="-mt-1 w-full text-base font-semibold tracking-wide md:text-lg truncate"> 
                                {accountName}
                            </h2>

                            { account.isFavorite &&
                                <span aria-hidden="true" className="block h-4 w-4 bg-light-blue-accent mask-[url('/icons/star-solid.svg')] mask-contain mask-center mask-no-repeat sm:h-5 sm:w-5"/>
                            }
                        </div>
                    </div>

                    {/* SALDON  */}
                    <div className="border border-dark-navy/50 bg-light-gray/40  pt-2 px-2">
                        <div className="mb-2">
                            <p>{t("overview-route.account-card.total-balance")}</p>
                            <p className="text-2xl font-bold -mt-1">
                                {formatCurrencySmallSek(account.balance, true)}
                            </p>
                        </div>

                        <div className="mb-2">
                            <div className="grid grid-cols-2 text-sm">
                                <span>{t("accounts-route.interest-section.accured-interest")}</span>
                                <span>{t("accounts-route.interest-section.yearly-interest")}</span>
                            </div>
                            <div className="grid grid-cols-2 text-xl font-bold -mt-0.5">
                                <p className={` ${account.accruedInterestYtd > 0 ? "text-green-700" : ""}`}>
                                    {formatCurrencySmallSek(account.accruedInterestYtd)}
                                </p>
                                <p>
                                    {formatCurrencySmallSek(account.estimatedYearEndInterest)}
                                </p>
                            </div>
                        </div>
                    </div>
                        
                    {/* SPARMÅL */}
                    { savingGoal && 
                        <div>
                            <span className="text-xs uppercase text-primary-hover">{t("generic.savings-goal")}</span>

                            <div className="grid grid-cols-[6fr_1fr]">
                                <p className="font-medium">{savingGoal?.title}</p> 
                                <p className="justify-self-end"><span className="font-bold">{savingGoalPercentage > 100 ? "100" : displayedPercentage}</span>%</p>
                            </div>

                            <div className="shrink-0 h-2 my-1 w-full overflow-hidden rounded-full bg-secondary/20">
                                <div role="progressbar" aria-label={String(savingGoal.title)} aria-valuemin={0} aria-valuemax={100} aria-valuenow={savingGoalPercentage} className={`h-full rounded-full ${barColor} transition-[width] duration-300`} style={{ width: `${savingGoalPercentage}%` }} />
                            </div>
                        </div>
                    }
                </section>

                {/* HANTERA KONTO */}
                <div className="mt-auto shrink-0 w-full bg-white border-t border-light-blue-accent flex justify-center py-1">
                    <button
                        onClick={() => navigate(`/accounts?account=${account.id}`)} 
                        className="group  text-primary-blue font-semibold transition hover:text-primary-hover cursor-pointer">
                            {t("generic.handle-account")}
                    </button>
                </div>
            </div>
        </OverviewCard>
    );
}