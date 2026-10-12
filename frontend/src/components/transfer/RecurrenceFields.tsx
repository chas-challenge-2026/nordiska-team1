import { useTranslation } from "react-i18next";
import type { RepeatInterval } from "./transferHelpers";

const REPEAT_INTERVALS: RepeatInterval[] = ["week", "month", "year"];

export type RecurrenceFieldsValue = {
    repeatInterval: RepeatInterval;
    onRepeatIntervalChange: (value: RepeatInterval) => void;
};

type RecurrenceFieldsProps = RecurrenceFieldsValue & {
    /** Prefix för id:n, så att fälten är unika när flera formulär finns på sidan. */
    name: string;
};

/** Intervallval för återkommande överföringar. */
export default function RecurrenceFields({
    name,
    repeatInterval,
    onRepeatIntervalChange,
}: RecurrenceFieldsProps) {
    const { t } = useTranslation();
    const intervalId = `${name}Interval`;

    return (
        <div className="grid grid-cols-1 items-start gap-5 sm:grid-cols-2">
            <div>
                <label
                    htmlFor={intervalId}
                    className="text-sm font-bold text-dark-navy first-letter:uppercase"
                >
                    {t("page-transfer.interval-label")}
                </label>
                <select
                    id={intervalId}
                    name={intervalId}
                    value={repeatInterval}
                    onChange={(e) =>
                        onRepeatIntervalChange(e.target.value as RepeatInterval)
                    }
                    className="mt-1 w-full rounded-md border border-nordiska-blue bg-white px-3 py-2"
                >
                    {REPEAT_INTERVALS.map((interval) => (
                        <option key={interval} value={interval}>
                            {t(`page-transfer.repeating.${interval}`)}
                        </option>
                    ))}
                </select>
            </div>
        </div>
    );
}
