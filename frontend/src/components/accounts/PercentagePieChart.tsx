import { Pie, PieChart, ResponsiveContainer } from "recharts";
import { useTranslation } from "react-i18next";


type PercentagePieChartProps = {
    percentage: number;
    isCompleted: boolean;
};

export default function PercentagePieChart({
    percentage,
    isCompleted,
}: PercentagePieChartProps) {
    const {t} = useTranslation();
    const safePercentage = Math.min(100, Math.max(0, percentage));

    const data = [
        { name: "Procent", value: safePercentage, fill: isCompleted ? "#16A34A" : "#EDA91B"},
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