import axiosInstance from "./axiosInstance";

export interface Transaction {
    id: number;
    accountId: number;
    type: "Deposit" | "Withdraw";
    amount: number;
    createdAt: string;
    label?: string;
    targetAccountId?: number;
    isPlanned?: boolean;
    plannedDate?: string;
    repeating?: string;
}

type NewTransaction = Omit<Transaction, "id" | "createdAt">;

export interface TransferPayload {
    sourceAccountId: number;
    targetAccountId: number;
    amount: number;
    label?: string;
}

export interface PlannedTransactionPayload {
    accountId: number;
    type: "Deposit" | "Withdraw";
    amount: number;
    plannedDate: string;
    label?: string;
    targetAccountId?: number;
    repeating?: string;
}

export async function getTransactions(): Promise<Transaction[]> {
    const res = await axiosInstance.get("/transactions");
    return res.data;
}

export async function createTransaction(payload: NewTransaction): Promise<Transaction> {
    const res = await axiosInstance.post("/transactions", payload);
    return res.data;
}

export async function transferFunds(payload: TransferPayload): Promise<Transaction> {
    const res = await axiosInstance.post("/transactions/transfer", payload);
    return res.data;
}

export async function createPlannedTransaction(payload: PlannedTransactionPayload): Promise<Transaction> {
    const res = await axiosInstance.post("/transactions/planned", payload);
    return res.data;
}

export async function cancelPlannedTransaction(id: number): Promise<void> {
    await axiosInstance.delete(`/transactions/planned/${id}`);
}
