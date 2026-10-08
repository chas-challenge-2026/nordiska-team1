import { useState } from "react";
import { useTranslation } from "react-i18next";
import {
    fromRepeating,
    parseCustomDays,
    toRepeating,
} from "../components/transfer/transferHelpers";
import type { RepeatInterval } from "../components/transfer/transferHelpers";

/**
 * State för "Återkommande" + intervall. Delas av överföringsformuläret och
 * redigeringen av planerade överföringar.
 * @param initialRepeating Befintligt repeating-värde, t.ex. "month" eller "days:14".
 */
export function useRecurrence(initialRepeating?: string) {
    const { t } = useTranslation();
    const initial = fromRepeating(initialRepeating);

    const [recurring, setRecurring] = useState(initial.recurring);
    const [repeatInterval, setRepeatInterval] = useState<RepeatInterval>(initial.interval);
    const [customDays, setCustomDays] = useState(initial.customDays);

    const customDaysValue = parseCustomDays(customDays);
    const customDaysError =
        recurring &&
        repeatInterval === "custom" &&
        customDays.trim() !== "" &&
        customDaysValue === null
            ? t("page-transfer.custom-days-error")
            : undefined;
    // undefined = inte återkommande, eller eget intervall utan giltigt antal dagar.
    const repeating = recurring
        ? toRepeating(repeatInterval, customDaysValue)
        : undefined;
    const isValid = !recurring || repeating !== undefined;

    const reset = () => {
        setRecurring(false);
        setRepeatInterval("month");
        setCustomDays("");
    };

    return {
        recurring,
        setRecurring,
        repeating,
        isValid,
        reset,
        fieldProps: {
            repeatInterval,
            onRepeatIntervalChange: setRepeatInterval,
            customDays,
            onCustomDaysChange: setCustomDays,
            customDaysError,
        },
    };
}
