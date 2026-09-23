import { Route, Routes } from "react-router";
import ProtectedRoute from "./ProtectedRoute";

// Open
import LoginPage from "../pages/LoginPage";
import LandingPage from "../pages/LandingPage";
// import CollapsiblePlayground from "../pages/CollapsiblePlayground";
import PageNotFound from "../pages/PageNotFound";
import ServerError from "../pages/ServerError";

//Protected
import DesktopLayout from "../layouts/DesktopLayout";
import OverviewPage from "../pages/OverviewPage";
import TransferPage from "../pages/TransferPage";
import TransactionsPage from "../pages/TransactionsPage";
import SettingsPage from "../pages/SettingsPage";
import AccountsPage from "../pages/AccountsPage";
import FaqPage from "../pages/FaqPage";

export default function AppRoutes() {
    return (
        <Routes>
            {/* <Route path="/dev/collapsible" element={<CollapsiblePlayground />} /> */}
            <Route path="/welcome" element={<LandingPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/inactive" element={<LandingPage inactive />} />
            <Route path="/logged-out" element={<LandingPage loggedOut />} />

            <Route path="/error-500" element={<ServerError />} />

            <Route path="*" element={<PageNotFound />} />

            {/* PROTECTED ROUTES HÄR */}
            <Route element={<ProtectedRoute />}>
                <Route path="/settings" element={<SettingsPage />} />
                <Route path="/help" element={<FaqPage />} />

                <Route path="/" element={<DesktopLayout />}>
                    <Route index element={<OverviewPage />} />
                    <Route path="/transfer" element={<TransferPage />} />
                    <Route
                        path="/transactions"
                        element={<TransactionsPage />}
                    />
                    <Route path="/accounts" element={<AccountsPage />} />
                </Route>
            </Route>
        </Routes>
    );
}
