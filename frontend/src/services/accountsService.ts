import axiosInstance from "./axiosInstance";

export interface Account {
    id: number;
    customerId: number;
    accountNumber: string;
    accountType: string;
    balance: number;
    interestRate: number;
    createdAt: string;
}

export async function getAccounts(): Promise<Account[]> {
    const res = await axiosInstance.get("/savingsaccounts");
    return res.data;
}
