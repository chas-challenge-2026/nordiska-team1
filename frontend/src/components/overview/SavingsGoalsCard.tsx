import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { formatCurrencySmallSek } from "../../utils/currency";
import { useNavigate } from "react-router";
import { useGetAllSavingsGoals, useGetAccounts } from "../../hooks/useAccounts";
import type { SavingGoal } from "../../services/accountsService";
import { LoadingState, ErrorState } from "../StatusMessage";
import OverviewCardHeader from "./OverviewCardHeader";
import { OverviewCardRow } from "./OverviewCardRow";

type SavingsGoalWithBalance = SavingGoal & {
    currentAmount: number;
};

function NoSavingGoalState() {
    const { t } = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-screen flex-col items-center justify-center -mt-5">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-10 w-10 bg-nordiska-orange mask-[url('/icons/target.svg')] mask-contain mask-center mask-no-repeat"/>
            </div>
            <p className="font-semibold mt-1 text-dark-navy/80"> {t("no-saving-goal-state.title")} </p>
            <p className="px-4 text-center text-[0.8em] mt-1 text-dark-navy/80"> {t("no-saving-goal-state.description")}</p>
            <p className="mt-4 text-center text-sm font-semibold text-dark-navy/80 md:mt-6">
            {t("no-saving-goal-state.action")}{" "}<span onClick={() => navigate("/accounts")} className="inline-block cursor-pointer text-primary-blue hover:text-primary-hover">{t("page-navigation.accounts")}</span></p>
        </div>
    );
}

export default function SavingsGoalsCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: savingsGoals, isLoading: goalsLoading, isError: goalsError } = useGetAllSavingsGoals();
    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts("active");

    const activeGoals: SavingsGoalWithBalance[] = (savingsGoals?.goals ?? [])
        .filter((goal) => goal.status === "active")
        .map((goal) => ({
            ...goal,
            currentAmount: accounts?.find((account) => account.id === goal.accountId)?.balance ?? 0,
        }));

    const getAccountNumber = (accountId: number | string | undefined ) => {
        const account = accounts?.find((account) => account.id === accountId);
        if (account?.status === "closed"){ return t("overview-route.transactions-card.closed-account")}
        return account?.accountNumber ?? "";
    };

    return (
        <OverviewCard>
            <div className="flex h-full flex-col ">

                {/* CARD HEADER */}
                <OverviewCardHeader title={t("overview-route.savings-card.title")} description={t("overview-route.savings-card.description")}/>

                {/* USER FEEDBACK */}
                {goalsLoading || accountsLoading && (<LoadingState message={t("overview-route.savings-card.loading")} />) }
                {goalsError || accountsError && (<ErrorState message={t("overview-route.savings-card.error")} />) }
                {activeGoals.length === 0 && (<NoSavingGoalState />)}

                {/* CONTENT ROWS */}
                {!goalsLoading && !accountsLoading && !goalsError && !accountsError && (
                    <section className="flex min-h-0 flex-1 flex-col ">
                        <div className="flex min-h-0 flex-1 flex-col overflow-hidden">
                            <div className="min-h-0 flex-1 overflow-y-auto">

                                {activeGoals.map((goal) => {

                                        const target = Math.max(0, goal.targetAmount);
                                        const saved = Math.max(0, goal.currentAmount);
                                        const remaining = Math.max(0, target - saved);
                                        const rawPercentage = target > 0 ? Math.min(100, (saved / target) * 100) : 0;
                                        const displayedPercentage = rawPercentage === 0
                                                ? "0"
                                                : rawPercentage < 1
                                                    ? rawPercentage.toLocaleString("sv-SE", { minimumFractionDigits: 1, maximumFractionDigits: 1, })
                                                    : Math.round(rawPercentage).toString();
                                
                                        return (
                                            <OverviewCardRow 
                                                key={goal.id}
                                                btnHoverText={`${t("overview-route.savings-card.btn-hover")} ${getAccountNumber(goal.accountId)}`}
                                                onClick={() => navigate(`/accounts?account=${goal.accountId}`)}
                                                cols={"3"}
                                                titleHoverText={goal.title}
                                                titleRow={[goal.title, <>{ displayedPercentage }<span className="font-light text-sm">%</span></>, <>{formatCurrencySmallSek(remaining)} <span className="text-xs">{t("overview-route.savings-card.remaining")} </span></>]}
                                                descriptionRow={[]}
                                                progressBar={[true, rawPercentage]}
                                            />
                                        )
                                })}
                            </div>
                        </div>
                    </section>
                )}
            </div>
        </OverviewCard>
    );
}