import { Navigate, Outlet } from "react-router";
import { useUserStore } from "../store/userStore";

export default function AdminRoute() {
    const user = useUserStore((state) => state.user);
    const loggedOut = useUserStore((state) => state.loggedOut);
    const loggedOutDueToInactivity = useUserStore((state) => state.loggedOutDueToInactivity);

    if (loggedOutDueToInactivity) { return <Navigate to="/inactive" replace />;}
    if (loggedOut) { return <Navigate to="/logged-out" replace />;}
    if (!user) {return <Outlet/>;}
    if (user?.role !== "Admin") { return <Navigate to="/" replace />}

    return <Outlet />
}