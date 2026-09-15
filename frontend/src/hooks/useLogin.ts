import { useMutation, useQuery } from "@tanstack/react-query";
import { login, checkSession, bankIdInitiate, bankIdCollect } from "../services/authService";
import { useUserStore } from "../store/userStore";
import { useEffect } from "react";


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
    }, [query.data]);

    return query;
}
