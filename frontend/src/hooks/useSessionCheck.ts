import { useEffect } from "react";
import { checkSession } from "../services/sessionsService";
import { useUserStore } from "../store/userStore";

export function useSessionCheck() {
    const setUser = useUserStore((state) => state.setUser);
    const setCheckingSession = useUserStore((state) => state.setCheckingSession);

    useEffect(() => {

        async function verifySession() {
            try {
                const user = await checkSession();
                setUser(user);
            } catch (error) {
                console.log("Kunde inte kontrollera sessionen:", error)
            } finally {
                setCheckingSession(false);
            }
        }
        
        verifySession();
        
    }, [setUser, setCheckingSession]);
}
