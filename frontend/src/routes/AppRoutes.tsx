import { Route, Routes } from "react-router";
import ProtectedRoute from "./ProtectedRoute";

// Open
import LandingPage from "../pages/LandingPage";
import CollapsiblePlayground from "../pages/CollapsiblePlayground";

//Protected
import DesktopLayout from "../layouts/DesktopLayout";
import OverviewPage from "../pages/OverviewPage";

export default function AppRoutes() {

    return (
        <Routes>

            {/* <Route path="/login" element={<LogInPage />} /> */}
            <Route path="/welcome" element={<LandingPage />} />
            <Route path="/inactive" element={<LandingPage inactive />} />
            <Route path="/dev/collapsible" element={<CollapsiblePlayground />} />

            {/* PROTECTED ROUTES HÄR */}
            <Route element={<ProtectedRoute/>}>
                <Route path="/" element={<DesktopLayout />}>
                    <Route index element={<OverviewPage />} />
                </Route>
            </Route>
        </Routes>
    )
}