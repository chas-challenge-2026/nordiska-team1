import { useQuery } from "@tanstack/react-query";
import { getAccounts } from "../services/accountsService";

export const accountKey = {
    all: ["accounts"] as const,
};

export function useAccounts() {
    return useQuery({
        queryKey: accountKey.all,
        queryFn: getAccounts,
    });
}
