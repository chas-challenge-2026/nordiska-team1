import axiosInstance from "./axiosInstance";

export type AccountTypes = "saving" | "standard" | "flex" | "fix" | "premium";

export type accountStatuses = "active" | "closed";

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
    status: accountStatuses;
    type: AccountTypes;
}

export async function getAllAccounts(status?: accountStatuses): Promise<Account[]> {
    const res = await axiosInstance.get("/accounts", { params: status ? { status } : undefined });
    return res.data;
}

export async function createAccount(
    customerId: number,
    accountName: string | null,
    accountType: AccountTypes,
    initialDeposit?: number,
): Promise<Account> {
    const res = await axiosInstance.post("/accounts", {
        customerId,
        accountName,
        accountType,
        initialDeposit,
    });
    return res.data;
}

export async function getAccount(id: number): Promise<Account> {
    const res = await axiosInstance.get(`/accounts/${id}`);
    return res.data;
}

export async function closeAccount(id: number): Promise<Account> {
    const res = await axiosInstance.post(`/accounts/${id}/close`, id);
    return res.data;
}

export async function getAccountTypes() {
    const res = await axiosInstance.get("/account-types");
    return res.data
}
