import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { login, register, checkSession, bankIdInitiate, bankIdCollect } from "../services/authService";
import type { RegisterCustomerRequest } from "../services/authService";
import { createAccount } from "../services/accountsService";
import { accountKey } from "./useAccounts";
import { useUserStore } from "../store/userStore";
import { useEffect } from "react";

// Namn och kontotyp för det sparkonto som skapas automatiskt åt varje ny kund.
// "Standard" är den enklaste av de kontotyper som är seedade i backend
// (se AccountTypeConfig). Startinsättning skickas inte med - backend sätter 0 som standard.
const DEFAULT_ACCOUNT_NAME = "Sparkonto";
const DEFAULT_ACCOUNT_TYPE = "Standard";


export function useLogin() {
    const setUser = useUserStore((state) => state.setUser);

    return useMutation({
        mutationFn: ({ email, password }: { email: string; password: string }) =>
            login(email, password),
        onSuccess: async () => {
            const user = await checkSession();
            setUser(user);
        },
    });
}

export function useRegister() {
    const setUser = useUserStore((state) => state.setUser);
    const queryClient = useQueryClient();

    return useMutation({
        mutationFn: async (data: RegisterCustomerRequest) => {
            await register(data);
            const user = await checkSession();
            setUser(user);

            // Varje ny kund ska ha ett konto direkt. Detta är ett riktigt
            // anrop mot backend (inte simulerat) - samma endpoint som
            // "Nytt konto" på Bankkonton-sidan använder.
            let accountCreated = true;
            if (user) {
                try {
                    await createAccount(user.id, DEFAULT_ACCOUNT_NAME, DEFAULT_ACCOUNT_TYPE);
                    queryClient.invalidateQueries({ queryKey: accountKey.all });
                } catch (err) {
                    // Kunden är redan registrerad och inloggad - det ska inte
                    // stoppas av att det automatiska kontot inte kunde skapas.
                    accountCreated = false;
                    console.error("Kunde inte skapa sparkonto automatiskt vid registrering.", err);
                }
            }

            return { user, accountCreated };
        },
    });
}

export function useBankIdInitate() {
    return useMutation({
        mutationFn: (personalNum: string) => bankIdInitiate(personalNum),
    });
};


export function useBankIdCollect(orderRef: string) {
    const setUser = useUserStore((state) => state.setUser);

    const query = useQuery({
        queryKey: ["bankIdCollect", orderRef],

        queryFn: () => bankIdCollect(orderRef!),

        enabled: !!orderRef,
        retry: false,

        refetchInterval: (query) => {
            const status = query.state.data?.status;

            if (status === "COMPLETE") {
                return false;
                // THROWA ERROR HÄR, MEN FÖRST ORDENTLIG STATUS TILLBAKA FRÅN BACKEND
            }

            if (query.state.error) {
                return false;
            }

            return 200;
        },
    });

    useEffect(() => {
        if (query.data?.status === "COMPLETE" && query.data.customer) {
            setUser(query.data.customer);
        }
    }, [query.data, setUser]);

    return query;
}
