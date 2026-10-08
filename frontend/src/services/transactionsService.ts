import axiosInstance from "./axiosInstance";

export interface Transaction {
    id: number;
    accountId: number;
    type: "deposit" | "withdrawal" | "transfer";
    amount: number;
    createdAt: string;
    label?: string;
    targetAccountId?: number;
    isPlanned?: boolean;
    plannedDate?: string;
    repeating?: string;
}

export interface TransactionParams {
    AccountId?: number;
    AccountIds?: number[];
    Type?: Transaction["type"];
    FromDate?: string;
    ToDate?: string;
    MinAmount?: number;
    MaxAmount?: number;
    SearchTerm?: string;
    SortBy?: string;
    SortOrder?: string;
    Page: number;
    PageSize: number;
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

export interface PagedResult<T> {
    items: T[];
    totalCount: number;
    page: number;
    pageSize: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export async function getTransactions(input: TransactionParams): Promise<PagedResult<Transaction>> {
    const res = await axiosInstance.get("/transactions", { params: input, paramsSerializer: { indexes: null } });
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
