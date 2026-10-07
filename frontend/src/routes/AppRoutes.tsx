import { Route, Routes } from "react-router";
import CustomerRoute from "./CustomerRoute";
import AdminRoute from "./AdminRoute";

// Open
import LoginPage from "../pages/LoginPage";
import LandingPage from "../pages/LandingPage";
import RegisterPage from "../pages/RegisterPage";
import PageNotFound from "../pages/PageNotFound";
import ServerError from "../pages/ServerError";

// Customer
import DesktopLayout from "../layouts/BaseLayout";
import OverviewPage from "../pages/OverviewPage";
import TransferPage from "../pages/TransferPage";
import TransactionsPage from "../pages/TransactionsPage";
import SettingsPage from "../pages/SettingsPage";
import AccountsPage from "../pages/AccountsPage";
import FaqPage from "../pages/FaqPage";
import InboxPage from "../pages/InboxPage";

// Admin
import Admin from "../pages/admin/Admin";
import FaqAdmin from "../pages/admin/faq/Faq";
import CreateAndUpdateFaq from "../pages/admin/faq/CreateAndUpdateFaq";
import EditFaq from "../pages/admin/faq/EditFaq";
import DeleteFaq from "../pages/admin/faq/DeleteFaq";
import AdminInbox from "../pages/admin/inbox/AdminInbox";

export default function AppRoutes() {
    return (
        <Routes>
            {/* OPEN ROUTES HÄR */}
            <Route path="/welcome" element={<LandingPage />} />
            <Route path="/login" element={<LoginPage />} />
            <Route path="/register" element={<RegisterPage />} />
            <Route path="/inactive" element={<LandingPage inactive />} />
            <Route path="/logged-out" element={<LandingPage loggedOut />} />

            <Route path="*" element={<PageNotFound />} />
            <Route path="/error-500" element={<ServerError />} />

            {/* PROTECTED CUSTOMER ROUTES HÄR */}
            <Route element={<CustomerRoute />}>
                <Route path="/settings" element={<SettingsPage />} />
                <Route path="/help" element={<FaqPage />} />

                <Route path="/" element={<DesktopLayout />}>
                    <Route index element={<OverviewPage />} />
                    <Route path="/transfer" element={<TransferPage />} />
                    <Route path="/transactions" element={<TransactionsPage />} />
                    <Route path="/accounts" element={<AccountsPage />} />
                    <Route path="/inbox/:threadId?" element={<InboxPage />} />
                </Route>
            </Route>

            {/* PROTECTED ADMIN ROUTES HÄR */}
            <Route element={<AdminRoute />}>
                <Route path="/admin">
                    <Route index element={<Admin />} />

                    <Route path="faq" element={<FaqAdmin />}>
                        <Route path="create" element={<CreateAndUpdateFaq />} />
                        <Route path="edit" element={<EditFaq />} />
                        <Route path="edit/form" element={<CreateAndUpdateFaq />} />
                        <Route path="delete" element={<DeleteFaq />} />
                    </Route>
                    <Route path="inbox" element={<AdminInbox />} />
                </Route>
            </Route>
        </Routes>
    );
}
