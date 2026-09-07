import { Navigate, Outlet } from "react-router";

export default function ProtectedRoute() {
    // ÄNDRA NÄR INLOGG ÄR PÅ PLATS
    // useAuth context? Eller useUser context?
    const isAuthenticated = true;

    if (!isAuthenticated) {
        return <Navigate to="/login" replace />;
    }

    return <Outlet />
}