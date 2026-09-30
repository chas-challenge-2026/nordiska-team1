import { Navigate, Outlet } from "react-router";
import { useUserStore } from "../store/userStore";

export default function ProtectedRoute() {
    const user = useUserStore((state) => state.user);
    const loggedOut = useUserStore((state) => state.loggedOut);
    const loggedOutDueToInactivity = useUserStore((state) => state.loggedOutDueToInactivity);

    if (loggedOutDueToInactivity) {
        return <Navigate to="/inactive" replace />;
    } else if (loggedOut) {
        return <Navigate to="/logged-out" replace />;
    } else if (!user) {
        return <Navigate to="/welcome" replace />;
    }

    return <Outlet />
}
