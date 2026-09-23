const PLAIN = new Intl.NumberFormat(undefined, { style: "currency", currency: "SEK" });
const SIGNED = new Intl.NumberFormat(undefined, { style: "currency", currency: "SEK", signDisplay: "exceptZero" });

export function formatCurrency(value: number, { signed = false } = {}): string {
    return (signed ? SIGNED : PLAIN).format(value);
}
