import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { type AccountTypes, getAccount, getAllAccounts, createAccount, closeAccount } from "../services/accountsService";

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

export function useGetAccounts() {
    return useQuery({
        queryKey: accountKey.all,
        queryFn: getAllAccounts,
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
        mutationFn: (input: CreateAccountInput) => createAccount(input.customerId, input.accountName, input.accountType, input.initialDeposit, input.interestRate),
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: accountKey.all })
        },
    })
}

export function useCloseAccount() {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: (id: number) => closeAccount(id),
        onSuccess: (_, id) => {
            queryClient.invalidateQueries({queryKey: accountKey.all})
            queryClient.invalidateQueries({queryKey: accountKey.detail(id)})
        }
    })
}
