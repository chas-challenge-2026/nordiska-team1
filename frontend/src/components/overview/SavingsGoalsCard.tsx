import { useEffect, useState} from "react";
import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { formatCurrency } from "../../utils/currency";
import { useNavigate } from "react-router";
import { useGetAllSavingsGoals, useGetAccounts } from "../../hooks/useAccounts";
import type { SavingGoal } from "../../services/accountsService";
import { LoadingState, ErrorState } from "../StatusMessage";

type SavingsGoalWithBalance = SavingGoal & {
    currentAmount: number;
};

const STORAGE_KEY = "overview-selected-savings-goals";
const MAX_SELECTED = 4;

function getStoredIds(): number[] {
    try {
        const stored = localStorage.getItem(STORAGE_KEY);
        if (!stored) return [];
        const parsed: unknown = JSON.parse(stored);
        if (!Array.isArray(parsed)) return [];
        return [...new Set(parsed.filter((id): id is number => typeof id === "number" && Number.isFinite(id)))].slice(0, MAX_SELECTED);
    } catch {
        return [];
    }
}

function NoSavingGoalState() {
    const { t } = useTranslation();
    const navigate = useNavigate();

    return (
        <div className="flex h-full min-h-64 flex-col items-center justify-center py-8">
            <div className="flex w-28 items-center justify-center">
                <span aria-hidden="true" className="block h-15 w-15 bg-nordiska-orange mask-[url('/icons/target.svg')] mask-contain mask-center mask-no-repeat" />
            </div>
            <p className="mt-1 text-center font-semibold text-dark-navy/80 xl:text-lg">{t("no-saving-goal-state.title")}</p>
            <p className="mt-1 px-10 text-center text-sm text-dark-navy/80">{t("no-saving-goal-state.description")}</p>
            <p className="mt-4 text-center text-[0.9em] font-semibold text-dark-navy/80 md:mt-6 xl:text-base">
                {t("no-saving-goal-state.action")}{" "}
                <span onClick={() => navigate("/accounts")} className="inline-block cursor-pointer text-primary-blue hover:text-nordiska-blue">{t("page-navigation.accounts")}</span>
            </p>
        </div>
    );
}

function GoalProgress({ goal, index }: { goal: SavingsGoalWithBalance; index: number; }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const target = Math.max(0, goal.targetAmount);
    const saved = Math.max(0, goal.currentAmount);
    const percentage = target > 0 ? Math.min(100, Math.round((saved / target) * 100)) : 0;
    const remaining = Math.max(0, target - saved);

    return (
        <li 
            onClick={() => navigate(`/accounts?account=${goal.accountId}`)}
                role="link"
                tabIndex={0}
                onKeyDown={(event) => {
                    if (event.key === "Enter" || event.key === " ") {
                        event.preventDefault();
                        navigate(`/accounts?account=${goal.accountId}`);
                    }
                }}
            className={`flex basis-1/4 shrink-0 flex-col justify-center border-nordiska-orange px-3 py-2 even:bg-light-gray/60 odd:bg-white sm:px-5 cursor-pointer hover:bg-primary-blue/10 ${index === 3 ? "last:border-0" : "border-b-2"}`}>

            <p className="flex items-baseline justify-between gap-2 text-sm sm:text-base">
                <span className="min-w-0 wrap-break-word font-semibold">{goal.title} </span>
                <span className="text-xs"><span className="font-semibold">{ percentage }% </span>{t("overview-route.savings-card.achieved")}</span>
            </p>

            <div className=" h-2 w-full overflow-hidden rounded-full bg-secondary/30">
                <div role="progressbar" aria-label={goal.title} aria-valuemin={0} aria-valuemax={100} aria-valuenow={percentage} className={`h-full rounded-full ${percentage === 100 ? "bg-success" : "bg-nordiska-orange"} transition-[width] duration-300`} style={{ width: `${percentage}%` }} />
            </div>

            <p className="flex mt-1.5 flex-wrap justify-between gap-x-2 gap-y-1 text-[9px] text-dark-navy/60 sm:text-xs">
                <span className="min-w-0 wrap-break-word"> <span className="font-semibold">{formatCurrency(saved)}</span> / {formatCurrency(target)} </span>
                <span className="shrink-0 whitespace-nowrap">{formatCurrency(remaining)} {t("overview-route.savings-card.remaining")}</span>
            </p>
        </li>
    );
}

export default function SavingsGoalsCard() {
    const { t } = useTranslation();
    const { data: savingsGoals, isLoading: goalsLoading, isError: goalsError } = useGetAllSavingsGoals();
    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts("active");
    const [selectedIds, setSelectedIds] = useState<number[]>(getStoredIds);
    const [isDropdownOpen, setIsDropdownOpen] = useState(false);

    const activeGoals: SavingsGoalWithBalance[] = (savingsGoals?.goals ?? [])
        .filter((goal) => goal.status === "active")
        .map((goal) => ({
            ...goal,
            currentAmount: accounts?.find((account) => account.id === goal.accountId)?.balance ?? 0,
        }));

    const activeIds = new Set(activeGoals.map((goal) => goal.id));

    const validSelectedIds = selectedIds
        .filter((id) => activeIds.has(id))
        .slice(0, MAX_SELECTED);

    const selectedGoals = activeGoals.filter((goal) =>
        validSelectedIds.includes(goal.id)
    );

    useEffect(() => {
        if (goalsLoading || goalsError || !savingsGoals?.goals) return;

        localStorage.setItem(
            STORAGE_KEY,
            JSON.stringify(validSelectedIds)
        );
    }, [validSelectedIds, goalsLoading, goalsError, savingsGoals]);

    function toggleGoal(id: number) {
        setSelectedIds((previous) => {
            if (previous.includes(id)) {
                return previous.filter((selectedId) => selectedId !== id);
            }

            if (previous.length >= MAX_SELECTED) return previous;
            return [...previous, id];
        });
    }

    return (
        <OverviewCard>
            <div className="flex h-full flex-col ">
                <div className="relative flex items-end justify-between h-22 bg-dark-navy pl-2 pt-1.5 text-white sm:pl-3">
                    
                    <div className="flex flex-col items-start p-3 sm:p-4">
                        <h2 className="text-xl font-semibold tracking-wide sm:text-2xl"> {t("overview-route.savings-card.title")}</h2>
                        <p className="text-[0.65em] uppercase text-white/80 sm:text-[0.70em]">{t("overview-route.savings-card.description")}</p>
                    </div>

                    <div className="mr-3 mb-4">
                        <button type="button" aria-expanded={isDropdownOpen} aria-controls="savings-goals-dropdown" onClick={() => setIsDropdownOpen((open) => !open)} className="flex cursor-pointer items-center justify-between gap-2 rounded-lg border border-white/25 bg-white/10 px-2 py-1.5 text-left text-xs text-white hover:bg-white/15 focus:outline-none focus:ring-2 focus:ring-white">
                            
                                <span className="block font-semibold">{t("overview-route.savings-card.select")}</span>
                                <svg
                                    className={`h-3 w-3 transition-transform duration-200 ${
                                        isDropdownOpen ? "rotate-180" : ""
                                    }`}
                                    viewBox="0 0 12 8"
                                    fill="none"
                                    >
                                    <path
                                        d="M1 1L6 6L11 1"
                                        stroke="currentColor"
                                        strokeWidth="2"
                                        strokeLinecap="round"
                                        strokeLinejoin="round"
                                    />
                                    </svg>
                        </button>

                        {isDropdownOpen && (
                            <div id="savings-goals-dropdown" className="absolute right-0 top-full z-30 max-h-56 w-52 overflow-y-auto border border-light-gray bg-white p-1.5 text-dark-navy shadow-2xl">

                                <div className="border-b border-light-gray ">
                                    <p className="px-2 py-2 text-[10px] text-secondary">{t("overview-route.savings-card.max-goals", {max: MAX_SELECTED,})}</p>
                                </div>

                                {activeGoals.map((goal) => {
                                    const isSelected = validSelectedIds.includes(goal.id);
                                    const isDisabled = !isSelected && validSelectedIds.length >= MAX_SELECTED;     

                                    return (
                                        <label key={goal.id} className={`flex items-center gap-2 px-2 py-2 text-xs ${isDisabled ? "cursor-not-allowed opacity-50" : "cursor-pointer hover:bg-light-gray/50"}`}>
                                            <input type="checkbox" checked={isSelected} disabled={isDisabled} onChange={() => toggleGoal(goal.id)} className="size-3.5 shrink-0 accent-nordiska-orange" />
                                            <span className="min-w-0 flex-1 wrap-break-word">{goal.title}</span>
                                        </label>
                                    );
                                })}
                                <div className="flex w-full justify-end border-t border-light-gray ">
                                    <button type="button" onClick={() => setIsDropdownOpen(false)} className="uppercase rounded-md text-xs font-semibold p-2 cursor-pointer text-primary-blue hover:text-nordiska-blue">{t("overview-route.savings-card.done")}</button>
                                </div>
                            </div>

                        )}
                    </div>
                </div>

                <div className="flex flex-1 flex-col p-2 sm:p-3">
                    {goalsLoading || accountsLoading ? (
                        <LoadingState message={t("overview-route.savings-card.loading")} />
                    ) : goalsError || accountsError ? (
                        <ErrorState message={t("overview-route.savings-card.error")} />
                    ) : activeGoals.length === 0 ? (
                        <NoSavingGoalState />
                    ) : selectedGoals.length > 0 ? (
                        <ul className="min-w-0 overflow-hidden flex flex-col flex-1">
                            {selectedGoals.map((goal, index) => (
                                <GoalProgress key={goal.id} goal={goal} index={index} />
                            ))}
                        </ul>
                    ) : (
                        <p className="py-6 text-center text-sm text-secondary">{t("overview-route.savings-card.empty-selection")}</p>
                    )}
                </div>
            </div>
        </OverviewCard>
    );
}
