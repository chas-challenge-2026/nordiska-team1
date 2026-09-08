import { Navigate, Outlet } from "react-router";

export default function ProtectedRoute() {
    // ÄNDRA NÄR INLOGG ÄR PÅ PLATS
    // useAuth context? Eller useUser context?
    const isAuthenticated = sessionStorage.getItem("token");

    if (!isAuthenticated) {
        return <Navigate to="/welcome" replace />;
    }

    return <Outlet />
}
