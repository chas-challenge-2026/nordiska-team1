import { Pie, PieChart, ResponsiveContainer } from "recharts";
import { useTranslation } from "react-i18next";

type PercentagePieChartProps = {
    percentage: number;
};

const COLOR_STOPS = [
    { at: 0, color: [249, 115, 22] }, // #F97316
    { at: 40, color: [245, 158, 11] }, // #F59E0B
    { at: 80, color: [234, 179, 8] }, // #EAB308
    { at: 99, color: [204, 199, 3] },   
    { at: 100, color: [22, 163, 74] },  // #16A34A
];

function getProgressColor(percentage: number): string {
    const p = Math.min(100, Math.max(0, percentage));

    const upperIndex = COLOR_STOPS.findIndex((stop) => p <= stop.at);
    const upper = COLOR_STOPS[upperIndex === -1 ? COLOR_STOPS.length - 1 : upperIndex];
    const lower = COLOR_STOPS[Math.max(0, upperIndex - 1)];

    if (upper.at === lower.at) { return `rgb(${upper.color.join(",")})`;}
    const t = (p - lower.at) / (upper.at - lower.at);
    const [r, g, b] = lower.color.map((c, i) => Math.round(c + (upper.color[i] - c) * t));

    return `rgb(${r}, ${g}, ${b})`;
}

export default function PercentagePieChart({ percentage }: PercentagePieChartProps) {
    const { t } = useTranslation();
    const safePercentage = Math.min(100, Math.max(0, percentage));

    const data = [
        { name: "Procent", value: safePercentage, fill: getProgressColor(safePercentage) },
        { name: "Återstår", value: 100 - safePercentage, fill: "#E5E7EB" },
    ];

    return (
        <div className="relative h-40 w-40">
            <ResponsiveContainer width="100%" height="100%">
                <PieChart>
                    <Pie
                        data={data}
                        dataKey="value"
                        nameKey="name"
                        cx="50%"
                        cy="50%"
                        innerRadius="75%"
                        outerRadius="100%"
                        startAngle={90}
                        endAngle={-270}
                        stroke="none"
                        isAnimationActive
                        animationDuration={1000}
                        animationEasing="ease-out"
                        label={false}
                        labelLine={false}
                    />
                </PieChart>
            </ResponsiveContainer>

            <div className="absolute inset-0 flex flex-col items-center justify-center">
                <span className="text-2xl font-bold tracking-tight text-dark-navy">
                    {Math.round(safePercentage)}%
                </span>
                <span className="text-[10px] font-medium uppercase tracking-wide text-secondary">
                    {t("accounts-route.savings-goal.pie-chart")}
                </span>
            </div>
        </div>
    );
}