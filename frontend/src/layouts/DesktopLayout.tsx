import { Outlet } from "react-router";
import PageHeader from "../components/PageHeader";

export default function DesktopLayout() {
    return (
        <div className="h-screen flex flex-col">
            <PageHeader />
            <div className="flex flex-1 min-h-0 w-screen">
                <Outlet />
            </div>
        </div>
    )
}
