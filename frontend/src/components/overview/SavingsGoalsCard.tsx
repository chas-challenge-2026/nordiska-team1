import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import Table from "../Table";
import { formatCurrency } from "../../utils/currency";

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

/** Share of total reached, 0–100. Rounded down, so 100 only when actually reached. */
function toPercent(value: number, total: number): number {
    if (!Number.isFinite(value) || !Number.isFinite(total) || total <= 0) return 0;
    return Math.min(100, Math.max(0, Math.floor((value / total) * 100)));
}

function SavingsDonut({ percent, label }: { percent: number; label: string }) {
    const text = (percent / 100).toLocaleString(undefined, { style: "percent" });

    // Inline style: runtime percent. Conic gradient starts at top, runs clockwise.
    return (
        <div
            role="img"
            aria-label={label}
            className="relative size-24 shrink-0 rounded-full"
            style={{ background: `conic-gradient(var(--color-nordiska-orange) ${percent}%, var(--color-light-gray) 0)` }}
        >
            <span
                aria-hidden="true"
                className="absolute inset-2.5 flex items-center justify-center rounded-full bg-white text-base font-semibold"
            >
                {text}
            </span>
        </div>
    );
}

function GoalProgress({ goal }: { goal: SavingsGoal }) {
    const { t } = useTranslation();

    return (
        <li className="min-w-0 border-b border-light-gray py-3 first:pt-0 last:border-b-0 last:pb-0 sm:py-4">
            {/* flex-wrap: amounts drop below name when row too narrow. */}
            <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
                <span className="min-w-0 wrap-break-word text-sm font-semibold sm:text-base">{goal.name}</span>
                <span className="text-xs text-secondary sm:text-sm">
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
            <p className="mt-1 text-[10px] text-secondary sm:text-xs">
                {t("overview-route.savings-eta", { time: goal.etaLabel })}
            </p>
        </li>
    );
}

export default function SavingsGoalsCard() {
    const { t } = useTranslation();
    const { totalSaved, nextGoal, goals } = MOCK_SAVINGS;
    const percent = toPercent(totalSaved, nextGoal);

    return (
        <OverviewCard>
            <Table tableType="savings">
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
            </Table>
        </OverviewCard>
    );
}
