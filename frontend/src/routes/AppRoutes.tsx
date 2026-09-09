import { Route, Routes } from "react-router";
import ProtectedRoute from "./ProtectedRoute";

// Open
import LoginPage from "../pages/LoginPage";
import LandingPage from "../pages/LandingPage";
import CollapsiblePlayground from "../pages/CollapsiblePlayground";
import PageNotFound from "../pages/PageNotFound";

//Protected
import DesktopLayout from "../layouts/DesktopLayout";
import OverviewPage from "../pages/OverviewPage";
import TransactionsPage from "../pages/TransactionsPage";

export default function AppRoutes() {

    return (
        <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/welcome" element={<LandingPage />} />
            <Route path="/inactive" element={<LandingPage inactive />} />
            <Route path="/dev/collapsible" element={<CollapsiblePlayground />} />
            <Route path="*" element={<PageNotFound />} />

            {/* PROTECTED ROUTES HÄR */}
            <Route element={<ProtectedRoute/>}>
                <Route path="/" element={<DesktopLayout />}>
                    <Route index element={<OverviewPage />} />
                    <Route path="/transactions" element={<TransactionsPage />} />
                </Route>
            </Route>
        </Routes>
    )
}
