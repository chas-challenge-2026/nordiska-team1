import { useEffect } from "react";
import { checkSession } from "../services/authService";
import { useUserStore } from "../store/userStore";

export function useSessionCheck() {
    const setUser = useUserStore((state) => state.setUser);
    const setCheckingSession = useUserStore((state) => state.setCheckingSession);

    useEffect(() => {
        checkSession().then((user) => {
            setUser(user);
            setCheckingSession(false);
        });
    }, []);
}
