export type AccountKind = "bg" | "bank";

export type OwnAccount = {
    id: string;
    own: true;
    type: string;
    number: string;
    name: string;
    meta: string;
    balance: number;
};

export type Payee = {
    id: string;
    own: false;
    kind: AccountKind;
    name: string;
    meta: string;
};

export type TransferAccount = OwnAccount | Payee;

export type PlannedTransfer = {
    date: string;
    name: string;
    note: string;
    sum: number;
};

export const OWN_ACCOUNTS: OwnAccount[] = [
    { id: "a1", own: true, type: "Bankkonto", number: "NKM-20001", name: "Lön Baby", meta: "Bankkonto NKM-20001", balance: 47225.0 },
    { id: "a2", own: true, type: "Sparkonto", number: "NKM-211-8", name: "Yoghurtfonden", meta: "Sparkonto NKM-211-8", balance: 23657.08 },
    { id: "a3", own: true, type: "Sparkonto", number: "903 578 211-8", name: "Köp ny tv", meta: "Sparkonto 903 578 211-8", balance: 3.56 },
    { id: "a4", own: true, type: "Fondkonto", number: "903 578 211-8", name: "Spend when old", meta: "Fondkonto 903 578 211-8", balance: 310433.56 },
];

export const BG_PG_PAYEES: Payee[] = [
    { id: "b1", own: false, kind: "bg", name: "Skatteverket", meta: "Bankgiro 202-7001" },
    { id: "b2", own: false, kind: "bg", name: "Hyresvärden Fastighets AB", meta: "Bankgiro 5051-6363" },
    { id: "b3", own: false, kind: "bg", name: "Vattenfall", meta: "Plusgiro 41 82 68-2" },
];

export const BANK_PAYEES: Payee[] = [
    { id: "c1", own: false, kind: "bank", name: "Olle Persson", meta: "Bankkonto 8327-9, 924 336 1" },
    { id: "c2", own: false, kind: "bank", name: "Anna Lind", meta: "Bankkonto 6123, 456 789 012" },
];

export const FAVORITE_ACCOUNT_IDS: string[] = ["a2", "b2", "c1"];

export const PLANNED_TRANSFERS: PlannedTransfer[] = [
    { date: "2026-09-01", name: "Hyra", note: "Till Hyresvärden Fastighets AB", sum: 7400.0 },
    { date: "2026-09-15", name: "Pensionssparande", note: "Återkommande varje månad", sum: 150.0 },
    { date: "2026-09-25", name: "Spara till Yoghurtfonden", note: "Mellan egna konton", sum: 1500.0 },
    { date: "2026-10-05", name: "Olles yoghurt-fond", note: "Till Olle Persson", sum: 450.0 },
];
