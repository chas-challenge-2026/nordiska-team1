import { useMutation } from "@tanstack/react-query";
import { login, checkSession } from "../services/authService";
import { useUserStore } from "../store/userStore";

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
