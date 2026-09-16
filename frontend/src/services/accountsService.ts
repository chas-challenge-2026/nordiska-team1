import axiosInstance from "./axiosInstance";

export type AccountTypes = "saving" | "standard" | "flex" | "fix" | "premium";

export interface Account {
    id: number;
    customerId: number;
    accountNumber: string;
    accountType: string;
    balance: number;
    interestRate: number;
    createdAt: string;
    accountName: string;
    updatedAt: string;
    status: string;
    type: AccountTypes;
}

export async function getAccounts(): Promise<Account[]> {
    const res = await axiosInstance.get("/accounts");
    return res.data;
}

export async function createAccount(
    accountName: string | null,
    accountType: AccountTypes,
    initialDeposit?: number,
    interestRate?: number
): Promise<Account> {
    const res = await axiosInstance.post("/accounts", {
        accountName,
        accountType,
        initialDeposit,
        interestRate
    });
    return res.data;
}

export async function getAccount(id: number): Promise<Account> {
    const res = await axiosInstance.get(`/accounts/${id}`);
    return res.data;
}

export async function closeAccount(id: number): Promise<Account> {
    const res = await axiosInstance.post(`/accounts/${id}/close`);
    return res.data;
}

//export async function closeAccountWithPost(id: number): Promise<Account> {
//    const res = await axiosInstance.post(`/accounts/${id}/close`);
//    return res.data;
//}
