import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { type AccountTypes, type accountStatuses, getAccount, getAllAccounts, createAccount, closeAccount, getAccountTypes, updateFavoriteAccount } from "../services/accountsService";
import { getAllSavingsGoals, createSavingsGoal, updateSavingsGoal, deleteSavingsGoal } from "../services/accountsService";
interface CreateAccountInput {
    customerId: number;
    accountName: string | null;
    accountType: AccountTypes;
    initialDeposit?: number;
    interestRate?: number;
}

export const accountKey = {
    all: ["accounts"] as const,
    detail: (id: number) => [...accountKey.all, id] as const,
};

export const accountTypesKey = {
    all: ["account-types"] as const,
}

export const savingGoalKey = {
    all: ["saving-goals"] as const,
    detail: (accountId: number) =>
        [...savingGoalKey.all, accountId] as const,
};

export function useGetAccounts(status?: accountStatuses) {
    return useQuery({
        queryKey: [...accountKey.all, status],
        queryFn: () => getAllAccounts(status),
    });
}

export function useGetAccount(id: number) {
    return useQuery({
        queryKey: accountKey.detail(id),
        queryFn: () => getAccount(id),
        enabled: !!id,
    })
}

export function useCreateAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (input: CreateAccountInput) => createAccount(input.customerId, input.accountName, input.accountType, input.initialDeposit),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: accountKey.all })
        },
    })
}

export function useCloseAccount() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: (id: number) => closeAccount(id),

        onSuccess: async (_, id) => {
            const { goals } = await getAllSavingsGoals();
            const accountGoals = goals.filter((goal) => goal.accountId === id && goal.status !== "completed");

            await Promise.all(
                accountGoals.map((goal) =>
                    updateSavingsGoal({
                        id: goal.id,
                        status: "completed",
                    })
                )
            );

            await queryClient.invalidateQueries({queryKey: accountKey.all,});
            await queryClient.invalidateQueries({queryKey: accountKey.detail(id),});
            await queryClient.invalidateQueries({queryKey: savingGoalKey.all,});
        },
    });
}

export function useAccountTypes() {
    return useQuery({
        queryKey: accountTypesKey.all,
        queryFn: getAccountTypes,
    })
}

export function useUpdateFavoriteAccount() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: ({
            id,
            isFavorite,
        }: {
            id: number;
            isFavorite: boolean;
        }) => updateFavoriteAccount(id, isFavorite),

        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: accountKey.all,
            });
        },
    });
}

export function useGetAllSavingsGoals() {
    return useQuery({
        queryKey: savingGoalKey.all,
        queryFn: getAllSavingsGoals,
    });
}

export function useCreateSavingsGoal() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: createSavingsGoal,
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: savingGoalKey.all,
            });
        },
    });
}

export function useUpdateSavingsGoal() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: updateSavingsGoal,
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: savingGoalKey.all,
            });
        },
    });
}

export function useDeleteSavingsGoal() {
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: deleteSavingsGoal,
        onSuccess: () => {
            queryClient.invalidateQueries({
                queryKey: savingGoalKey.all,
            });
        },

    })
}