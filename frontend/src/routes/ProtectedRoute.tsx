import { Navigate, Outlet } from "react-router";
import { useUserStore } from "../store/userStore";

export default function ProtectedRoute() {
    const user = useUserStore((state) => state.user);
    const isAuthenticated = !!user;

    if (!isAuthenticated) {
        return <Navigate to="/welcome" replace />;
    }

    return <Outlet />
}
