import { useTranslation } from "react-i18next";
import { BarChart, Bar, XAxis, YAxis, ResponsiveContainer } from "recharts";

type ProgressBarProps =
    | {
        type: "time";
        createdAt: string;
        targetDate: string;
        color?: string;
    }
    | {
        type: "number";
        start: number;
        current: number;
        target: number;
        color?: string;
    };

function toUtcDay(dateString: string) {
    const date = new Date(dateString);

    return Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()) / 86_400_000;
}

export default function ProgressBar(props: ProgressBarProps) {
    const { t } = useTranslation();

    const { start, current, target, color } =
        props.type === "time"
            ? {
                start: toUtcDay(props.createdAt),
                current: toUtcDay(new Date().toISOString()),
                target: toUtcDay(props.targetDate),
                color: props.color ?? "#134663",
            }
            : {
                start: props.start,
                current: props.current,
                target: props.target,
                color: props.color ?? "#EDA91B",
            };

    const total = target - start;
    const isValid = total > 0;
    const progress = isValid ? Math.min(100, Math.max(0, ((current - start) / total) * 100)) : 0;
    const percentage = Math.round(progress);
    const elapsed = isValid ? Math.min(total, Math.max(0, current - start)) : 0;
    const remaining = isValid ? Math.max(0, target - current) : 0;

    const MIN_VISUAL_PROGRESS = 2;
    const visualPercentage = percentage === 0 && props.type === "time" ? MIN_VISUAL_PROGRESS : percentage;
    const data = [{ name: "Period", value: visualPercentage }];

    if (!isValid) {
        return (
            <p className="text-sm text-dark-navy/80">
                {props.type === "time" ? t("progress-bar.invalid-period") : t("progress-bar.invalid-goal")}
            </p>
        );
    }

    return (
        <div className="mt-5 w-full space-y-2">
            {props.type === "time" && (
                <div className="mb-1 ml-1 mt-8 flex flex-col items-start justify-between">
                    <p className="text-xl font-semibold text-dark-navy sm:text-xl">{props.targetDate.slice(0, 10)}</p>
                    <p className="mb-2 text-xs uppercase text-nordiska-blue sm:text-sm">{t("progress-bar.target-date-label")}</p>
                </div>
            )}

            <div className="flex flex-col justify-center px-1">
                <ResponsiveContainer width="100%" height={24}>
                    <BarChart data={data} layout="vertical" margin={{ top: 0, right: 0, bottom: 0, left: 0 }}>
                        <XAxis type="number" domain={[0, 100]} hide />
                        <YAxis type="category" dataKey="name" hide />
                        <Bar dataKey="value" fill={color} background={{ fill: "#E5E7EB", radius: 10 }} barSize={20} radius={10} isAnimationActive={false} />
                    </BarChart>
                </ResponsiveContainer>

                <div className="flex justify-between gap-2 px-1 text-sm font-medium text-dark-navy">
                    {props.type === "time" ? (
                        <>
                            <span>{elapsed === 0 ? t("progress-bar.just-started") : t("progress-bar.days-elapsed", { count: elapsed })}</span>
                            <span>{t("progress-bar.days-remaining", { count: remaining })}</span>
                        </>
                    ) : (
                        <>
                            <span>{t("progress-bar.amount-achieved", { amount: props.current - props.start })}</span>
                            <span>{t("progress-bar.amount-remaining", { amount: Math.max(0, props.target - props.current) })}</span>
                        </>
                    )}
                </div>

                <p className="mt-1 px-1 text-right text-sm text-dark-navy/80">{t("progress-bar.percentage", { percentage })}</p>
            </div>
        </div>
    );
}
