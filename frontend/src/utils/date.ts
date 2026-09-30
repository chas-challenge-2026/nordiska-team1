type DateInput = string | null | undefined;

function parse(value: DateInput): Date | null {
    if (!value) return null;
    const d = new Date(value);
    return Number.isNaN(d.getTime()) ? null : d;
}

/** Date in browser locale. Empty string on invalid input. */
export function formatDate(value: DateInput): string {
    return parse(value)?.toLocaleDateString() ?? "";
}

/** Time (hours + minutes) in browser locale. Empty string on invalid input. */
export function formatTime(value: DateInput): string {
    return parse(value)?.toLocaleTimeString(undefined, { hour: "2-digit", minute: "2-digit" }) ?? "";
}

/** Milliseconds since epoch. 0 on invalid input, so filter/sort never get NaN. */
export function toTimestamp(value: DateInput): number {
    return parse(value)?.getTime() ?? 0;
}

/**
 * Local date as YYYY-MM-DD, locale-free. For grouping and comparing (matches
 * <input type="date"> values), not for display. Empty string on invalid input.
 */
export function toDateKey(value: DateInput): string {
    const d = parse(value);
    if (!d) return "";
    const pad = (n: number) => String(n).padStart(2, "0");
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
