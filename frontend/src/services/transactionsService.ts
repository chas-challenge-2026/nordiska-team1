import axiosInstance from "./axiosInstance";

export interface Transaction {
    id: number;
    accountId: number;
    type: "Deposit" | "Withdraw";
    amount: number;
    createdAt: string;
}

type NewTransaction = Omit<Transaction, "id" | "createdAt">;

export async function getTransactions(): Promise<Transaction[]> {
    const res = await axiosInstance.get("/transactions");
    return res.data;
}

export async function createTransaction(payload: NewTransaction): Promise<Transaction> {
    const res = await axiosInstance.post("/transactions", payload);
    return res.data;
}
