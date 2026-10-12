import { useState } from "react";
import { fromRepeating } from "../components/transfer/transferHelpers";
import type { RepeatInterval } from "../components/transfer/transferHelpers";

/**
 * State för "Återkommande" + intervall. Delas av överföringsformuläret och
 * redigeringen av planerade överföringar.
 * @param initialRepeating Befintligt repeating-värde, t.ex. "month".
 */
export function useRecurrence(initialRepeating?: string) {
    const initial = fromRepeating(initialRepeating);

    const [recurring, setRecurring] = useState(initial.recurring);
    const [repeatInterval, setRepeatInterval] = useState<RepeatInterval>(initial.interval);

    // undefined = inte återkommande.
    const repeating = recurring ? repeatInterval : undefined;

    const reset = () => {
        setRecurring(false);
        setRepeatInterval("month");
    };

    return {
        recurring,
        setRecurring,
        repeating,
        reset,
        fieldProps: {
            repeatInterval,
            onRepeatIntervalChange: setRepeatInterval,
        },
    };
}
