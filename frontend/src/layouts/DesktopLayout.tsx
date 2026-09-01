import { Outlet } from "react-router";
import PageHeader from "../components/PageHeader";

export default function DesktopLayout() {
    return (
        <>
            <PageHeader />
            <Outlet />
        </>
    )
}
