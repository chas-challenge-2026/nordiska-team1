// Svenskt format med valutakoden efter beloppet, t.ex. "5 000,00 SEK"
const OPTIONS = { style: "currency", currency: "SEK", currencyDisplay: "code" } as const;
const PLAIN = new Intl.NumberFormat("sv-SE", OPTIONS);
const SIGNED = new Intl.NumberFormat("sv-SE", { ...OPTIONS, signDisplay: "exceptZero" });
const PLAIN_NO_CURRENCY = new Intl.NumberFormat("sv-SE", { style: "decimal", minimumFractionDigits: 2, maximumFractionDigits: 2 });
const SIGNED_NO_CURRENCY = new Intl.NumberFormat("sv-SE", { style: "decimal", minimumFractionDigits: 2, maximumFractionDigits: 2, signDisplay: "exceptZero"});

export function formatCurrency(value: number, { signed = false, showCurrency = true }: {signed?: boolean, showCurrency?: boolean} = {}): string {
    if(showCurrency) { return (signed ? SIGNED : PLAIN).format(value);}
    return (signed ? SIGNED_NO_CURRENCY : PLAIN_NO_CURRENCY).format(value);
}

export function formatCurrencySmallSek(value: number) {
    return (
        <span className="whitespace-nowrap">
            {formatCurrency(value, { showCurrency: false })}{" "}
            <span className="text-xs font-medium text-dark-navy">SEK</span>
        </span>
    );
}