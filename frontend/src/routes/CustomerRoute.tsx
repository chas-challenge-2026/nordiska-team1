import { Navigate, Outlet } from "react-router";
import { useUserStore } from "../store/userStore";

export default function CustomerRoute() {
    const user = useUserStore((state) => state.user);
    const loggedOut = useUserStore((state) => state.loggedOut);
    const loggedOutDueToInactivity = useUserStore((state) => state.loggedOutDueToInactivity);
    const isCheckingSession = useUserStore((state) => state.isCheckingSession);

    if (isCheckingSession) { return null; }
    if (loggedOutDueToInactivity) { return <Navigate to="/inactive" replace />;}
    if (loggedOut) { return <Navigate to="/logged-out" replace />;}
    if (!user) {return <Navigate to="/welcome" replace />;}
    if (user.role !== "Customer") { return <Navigate to="/admin" replace />}

    return <Outlet />
}