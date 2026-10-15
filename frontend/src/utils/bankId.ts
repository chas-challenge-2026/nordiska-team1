/**
 * Mobil eller surfplatta, oavsett skärmbredd. Behövs för att välja rätt sätt
 * att starta BankID-appen - ett smalt datorfönster ska fortfarande räknas som dator.
 */
export function isMobileDevice(): boolean {
    if (/Android|iPhone|iPad|iPod/i.test(navigator.userAgent)) return true;
    // iPadOS utger sig för att vara en Mac, men har pekskärm
    return navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1;
}

/**
 * Länk som startar BankID-appen på den här enheten. BankID rekommenderar
 * app.bankid.com på mobil och bankid:/// på dator.
 */
export function autoStartUrl(autoStartToken: string): string {
    const query = `autostarttoken=${autoStartToken}&redirect=null`;
    return isMobileDevice()
        ? `https://app.bankid.com/?${query}`
        : `bankid:///?${query}`;
}
