/**
 * Strips everything but digits, so "19790314-2380" and "790314+2380" both
 * end up as plain digits that the backend accepts.
 */
export function normalizePersonalNum(value: string): string {
    return value.replace(/\D/g, "");
}

/**
 * Checks that a Swedish personal number has 12 digits (the format the backend
 * and BankID match on) and passes the Luhn check, which is calculated on the
 * last 10 digits.
 */
export function isValidPersonalNum(value: string): boolean {
    const digits = normalizePersonalNum(value);
    if (digits.length !== 12) return false;

    const tenDigits = digits.slice(-10);
    let sum = 0;

    for (let i = 0; i < tenDigits.length; i++) {
        let n = Number(tenDigits[i]);
        if (i % 2 === 0) {
            n *= 2;
            if (n > 9) n -= 9;
        }
        sum += n;
    }

    return sum % 10 === 0;
}
