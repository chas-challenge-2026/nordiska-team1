// Svenskt format med valutakoden efter beloppet, t.ex. "5 000,00 SEK"
const OPTIONS = { style: "currency", currency: "SEK", currencyDisplay: "code" } as const;
const PLAIN = new Intl.NumberFormat("sv-SE", OPTIONS);
const SIGNED = new Intl.NumberFormat("sv-SE", { ...OPTIONS, signDisplay: "exceptZero" });

export function formatCurrency(value: number, { signed = false } = {}): string {
    return (signed ? SIGNED : PLAIN).format(value);
}
