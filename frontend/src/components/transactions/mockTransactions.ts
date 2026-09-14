export interface Account {
    id: string;
    name: string;
    balance: number;
}

export interface Transaction {
    id: number;
    title: string;
    account: string;
    accountLabel: string;
    date: string;
    time: string;
    amount: number;
}

export const accounts: Account[] = [
    { id: 'sparkonto-1234', name: 'Sparkonto — 1234', balance: 88210.50 },
    { id: 'sparkonto-5678', name: 'Sparkonto — 5678', balance: 12040.00 },
    { id: 'lonekonto-9012', name: 'Lönekonto — 9012', balance: 3450.90 },
    { id: 'buffertkonto-3456', name: 'Buffertkonto — 3456', balance: 150000.00 },
];

export const transactions: Transaction[] = [
    { id: 1, title: 'Pelles pub', account: 'sparkonto-1234', accountLabel: 'Konto ABC-123', date: '2026-08-26', time: '03:12', amount: -412.00 },
    { id: 2, title: 'Pelles pub', account: 'sparkonto-1234', accountLabel: 'Konto ABC-123', date: '2026-08-26', time: '03:12', amount: -412.00 },
    { id: 3, title: 'Överföring mellan konton', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-25', time: '15:22', amount: 23057.08 },
    { id: 4, title: 'Överföring mellan konton', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-25', time: '15:22', amount: 23057.08 },
    { id: 5, title: 'ICA Supermarket', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-24', time: '12:05', amount: -645.50 },
    { id: 6, title: 'Hyra', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-24', time: '00:01', amount: -12500.00 },
    { id: 7, title: 'Lön', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-23', time: '08:00', amount: 32000.00 },
    { id: 8, title: 'Spotify', account: 'sparkonto-5678', accountLabel: 'Konto DEF-345', date: '2026-08-22', time: '09:14', amount: -119.00 },
    { id: 9, title: 'Systembolaget', account: 'sparkonto-5678', accountLabel: 'Konto DEF-345', date: '2026-08-21', time: '18:40', amount: -890.00 },
    { id: 10, title: 'Överföring till buffert', account: 'buffertkonto-3456', accountLabel: 'Konto GHI-456', date: '2026-08-20', time: '10:00', amount: 5000.00 },
    { id: 11, title: 'Elräkning', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-19', time: '07:30', amount: -1230.00 },
    { id: 12, title: 'Restaurang Prego', account: 'sparkonto-1234', accountLabel: 'Konto ABC-123', date: '2026-08-18', time: '20:15', amount: -540.00 },
    { id: 13, title: 'Swish från Anna', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-17', time: '14:02', amount: 250.00 },
    { id: 14, title: 'Apoteket', account: 'sparkonto-5678', accountLabel: 'Konto DEF-345', date: '2026-08-16', time: '11:11', amount: -215.00 },
    { id: 15, title: 'Överföring mellan konton', account: 'buffertkonto-3456', accountLabel: 'Konto GHI-456', date: '2026-08-15', time: '09:45', amount: -2000.00 },
    { id: 16, title: 'H&M', account: 'sparkonto-1234', accountLabel: 'Konto ABC-123', date: '2026-08-14', time: '16:30', amount: -799.00 },
    { id: 17, title: 'Försäkring', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-13', time: '06:00', amount: -389.00 },
    { id: 18, title: 'Återbetalning Skatteverket', account: 'lonekonto-9012', accountLabel: 'Konto XYZ-234', date: '2026-08-12', time: '13:20', amount: 4500.00 },
    { id: 19, title: 'Café Rast', account: 'sparkonto-5678', accountLabel: 'Konto DEF-345', date: '2026-08-11', time: '08:55', amount: -65.00 },
    { id: 20, title: 'Gym', account: 'sparkonto-1234', accountLabel: 'Konto ABC-123', date: '2026-08-10', time: '17:00', amount: -399.00 },
];
