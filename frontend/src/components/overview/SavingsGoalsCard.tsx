import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { formatCurrency } from "../../utils/currency";
import { NoSavingGoalState } from "../StatusMessage";

type SavingsGoal = {
    id: string;
    name: string;
    saved: number;
    target: number;
    etaLabel: string;
};

type SavingsOverview = {
    totalSaved: number;
    nextGoal: number;
    goals: SavingsGoal[];
};

// Mock data. Replace with query hook later.
const MOCK_SAVINGS: SavingsOverview = {
    totalSaved: 43300,
    nextGoal: 50000,
    goals: [
        { id: "g1", name: "Yoghurtfonden", saved: 23657, target: 25000, etaLabel: "1 månad" },
        { id: "g2", name: "Köp ny tv", saved: 3.56, target: 8000, etaLabel: "4 år" },
        { id: "g3", name: "Spend when old", saved: 310434, target: 500000, etaLabel: "6 år" },
    ],
};

function toPercent(value: number, total: number): number {
    if (!Number.isFinite(value) || !Number.isFinite(total) || total <= 0) return 0;
    return Math.min(100, Math.max(0, Math.floor((value / total) * 100)));
}

function SavingsDonut({ percent, label }: { percent: number; label: string }) {
    const text = (percent / 100).toLocaleString(undefined, { style: "percent" });

    return (
        <div
            role="img"
            aria-label={label}
            className="relative size-18 shrink-0 rounded-full sm:size-22"
            style={{ background: `conic-gradient(var(--color-nordiska-orange) ${percent}%, var(--color-light-gray) 0)` }}
        >
            <span
                aria-hidden="true"
                className="absolute inset-2 flex items-center justify-center rounded-full bg-white text-sm font-semibold sm:inset-2.5 sm:text-base"
            >
                {text}
            </span>
        </div>
    );
}

function GoalProgress({ goal }: { goal: SavingsGoal }) {
    const { t } = useTranslation();

    return (
        <li className="min-w-0 border-b border-light-gray py-2 first:pt-0 last:border-b-0 last:pb-0 sm:py-2">
            {/* flex-wrap: amounts drop below name when row too narrow. */}
            <div className="flex flex-wrap items-baseline justify-between gap-x-2 gap-y-0.5 sm:gap-x-3">
                <span className="min-w-0 wrap-break-word text-xs font-semibold sm:text-base">{goal.name}</span>
                <span className="text-[10px] text-secondary sm:text-sm">
                    <span className="whitespace-nowrap font-semibold text-dark-navy">{formatCurrency(goal.saved)}</span>
                    {" / "}
                    <span className="whitespace-nowrap">{formatCurrency(goal.target)}</span>
                </span>
            </div>
            <progress
                value={goal.saved}
                max={goal.target}
                aria-label={goal.name}
                className="mt-2 block h-1.5 w-full appearance-none overflow-hidden rounded-full border-0 bg-light-gray [&::-moz-progress-bar]:rounded-full [&::-moz-progress-bar]:bg-nordiska-orange [&::-webkit-progress-bar]:bg-light-gray [&::-webkit-progress-value]:rounded-full [&::-webkit-progress-value]:bg-nordiska-orange"
            />
            <p className="mt-1 text-[9px] text-secondary sm:text-xs">
                {t("overview-route.savings-eta", { time: goal.etaLabel })}
            </p>
        </li>
    );
}

export default function SavingsGoalsCard() {
    const { t } = useTranslation();
    const { totalSaved, nextGoal, goals } = MOCK_SAVINGS;
    const percent = toPercent(totalSaved, nextGoal);
    const displayNone = true;

    return (
    <OverviewCard>
        <div className="flex h-full flex-col">

            {/* CARD HEADER  */}
            <div className="h-22 rounded-t-3xl bg-dark-navy pl-2 pt-1.5 text-white sm:pl-3">
                <div className="flex flex-col items-start p-3 sm:p-4">
                    <h2 className="text-xl font-semibold tracking-wide sm:text-2xl">{t("overview-route.savings-card.title")}</h2>
                    <p className="text-[0.65em] uppercase text-white/80 sm:text-[0.70em]"> {t("overview-route.savings-card.description")}  </p>
               </div>
            </div>

            {/* USER FEEDBACK */}
            {/* 
            {isPending && ( <LoadingState title={ SKRIV WHEN DONE } />)}
            {isError && ( <ErrorState title={ SKRIV WHEN DONE } />)} 
            */}
            {displayNone && ( <NoSavingGoalState />)} 

            {!displayNone && (
                <section className="flex flex-1 flex-col p-2 sm:p-3">
                <div className="flex flex-1 flex-col rounded-b-xl overflow-hidden">

                    <div className="flex h-full flex-col justify-between px-1 pb-2 sm:px-2">
                        <div className="flex items-center justify-center gap-2 border-b border-primary-blue px-2 pb-2.5 sm:gap-6 sm:px-5">
                            <div className="min-w-0">
                                <p className="text-[10px] text-secondary sm:text-sm">{t("overview-route.savings-total")}</p>
                                <p className="wrap-break-word text-lg font-semibold sm:text-2xl">{formatCurrency(totalSaved)}</p>
                                <p className="mt-2 wrap-break-word text-[10px] text-secondary sm:mt-3 sm:text-sm">
                                    {t("overview-route.savings-next-goal")}{" "}
                                    <span className="whitespace-nowrap font-semibold text-dark-navy">{formatCurrency(nextGoal)}</span>
                                </p>
                            </div>
                            <SavingsDonut percent={percent} label={t("overview-route.savings-progress-label", { percent })} />
                        </div>
                        <ul className="min-w-0">
                            {goals.map((goal) => (
                                <GoalProgress key={goal.id} goal={goal} />
                            ))}
                        </ul>
                    </div>
                </div>
                </section>
            )}
        </div>
    </OverviewCard>
    );
}

{/* <Table tableType="savings">
    <div className="flex items-center justify-between gap-3 sm:gap-4">
        <div className="min-w-0 flex-1">
            <p className="text-xs text-secondary sm:text-sm">{t("overview-route.savings-total")}</p>
            <p className="wrap-break-word text-xl font-semibold sm:text-2xl">{formatCurrency(totalSaved)}</p>
            <p className="mt-2 wrap-break-word text-xs text-secondary sm:mt-3 sm:text-sm">
                {t("overview-route.savings-next-goal")}{" "}
                <span className="whitespace-nowrap font-semibold text-dark-navy">{formatCurrency(nextGoal)}</span>
            </p>
        </div>
        <SavingsDonut percent={percent} label={t("overview-route.savings-progress-label", { percent })} />
    </div>
    <ul className="min-w-0">
        {goals.map((goal) => (
            <GoalProgress key={goal.id} goal={goal} />
        ))}
    </ul>
</Table> */}