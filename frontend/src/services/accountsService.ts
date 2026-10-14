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
    accruedInterestYtd: number;
    estimatedYearEndInterest: number;
    isFavorite: boolean;
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

export async function updateFavoriteAccount(id: number, isFavorite: boolean) {
    const res = await axiosInstance.put(`/accounts/${id}/favorite`, { isFavorite });
    return res.data;
}

// ------------------------
// ----- SAVING GOALS -----
// ------------------------
export type SavingGoalStatus = "active" | "completed";

export type CreateSavingGoalParams = {
    accountId: number,
    targetAmount: number,
    targetDate: string | null,
    title: string | null,
    status?: SavingGoalStatus
};

export async function createSavingsGoal(params: CreateSavingGoalParams) {
    const res = await axiosInstance.post("/savings-goals", params);
    return res.data;
}

export type SavingGoal = {
    id: number;
    accountId: number;
    createdAt: string;
    title: string;
    targetAmount: number;
    currentAmount: number;
    targetDate: string;
    status: string;
    progressPercentage: number;
    etaLabel: string;
    estimatedCompletionDate: string;
}

type SavingGoalsResponse = {
    goals: SavingGoal[];
    summary: {
        totalCurrentBalance: number;
        totalTargetAmount: number;
        totalProgressPercentage: number;
    }
}

export async function getAllSavingsGoals(): Promise<SavingGoalsResponse>  {
    const res = await axiosInstance.get("/savings-goals");
    return res.data;
}

type UpdateSavingGoalParams = {
    id: number;
    title?: string | null;
    targetAmount?: number | null;
    targetDate?: string | null;
    status?: SavingGoalStatus | null;
};

export async function updateSavingsGoal({    
    id,
    title,
    targetAmount,
    targetDate,
    status}:UpdateSavingGoalParams) {
    const res = await axiosInstance.patch(`/savings-goals/${id}`, {
        title,
        targetAmount,
        targetDate,
        status
    });
    return res.data;
}

export async function deleteSavingsGoal(id: number) {
    const res = await axiosInstance.delete(`/savings-goals/${id}`);
    return res.data;
}