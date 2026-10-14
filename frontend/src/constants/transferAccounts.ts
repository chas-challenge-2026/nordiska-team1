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
    localId: string;
    source: "backend" | "local";
    backendId?: number;
    date: string;
    name: string;
    note: string;
    sum: number;
    fromName?: string;
    toName?: string;
    accountId?: number;
    targetAccountId?: number;
    type?: string;
    label?: string;
    repeating?: string;
};
