import axiosInstance from "./axiosInstance";

export interface Transaction {
    id: number;
    accountId: number;
    type: "deposit" | "withdrawal" | "transfer" | "interest" | "tax";
    amount: number;
    createdAt: string;
    label?: string;
    targetAccountId?: number;
    isPlanned?: boolean;
    plannedDate?: string;
    repeating?: string;
    /** Delas av båda benen i en överföring mellan egna konton */
    correlationId?: string | null;
}

export interface TransactionParams {
    AccountId?: number;
    AccountIds?: number[];
    Type?: Transaction["type"];
    IsPlanned?: boolean;
    GroupByDate?: boolean;
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

/** Transaktioner för ett datum, grupperade av backend (date/dateKey är YYYY-MM-DD i UTC). */
export interface TransactionDateGroup {
    date: string;
    dateKey: string;
    items: Transaction[];
}

export interface TransactionsResult extends PagedResult<Transaction> {
    /** Finns bara när GroupByDate: true skickas med */
    groups?: TransactionDateGroup[];
}

export async function getTransactions(
    input: TransactionParams,
): Promise<TransactionsResult> {
    const res = await axiosInstance.get("/transactions", {
        params: input,
        paramsSerializer: { indexes: null },
    });
    return res.data;
}

export async function createTransaction(
    payload: NewTransaction,
): Promise<Transaction> {
    const res = await axiosInstance.post("/transactions", payload);
    return res.data;
}

export async function transferFunds(
    payload: TransferPayload,
): Promise<Transaction> {
    const res = await axiosInstance.post("/transactions/transfer", payload);
    return res.data;
}

export async function createPlannedTransaction(
    payload: PlannedTransactionPayload,
): Promise<Transaction> {
    const res = await axiosInstance.post("/transactions/planned", payload);
    return res.data;
}

export async function cancelPlannedTransaction(id: number): Promise<void> {
    await axiosInstance.delete(`/transactions/planned/${id}`);
}
