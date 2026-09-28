import { Outlet } from "react-router";
import PageNavigation from "../components/PageNavigation";
// import PageFooter from "../components/PageFooter";

export default function DesktopLayout() {
    return (
        <div className="h-dvh flex flex-col">
            <PageNavigation />

            {/* ANIMATION PÅ DETTA */}
            <main className="flex flex-1 min-h-0 w-screen mb-[60px] md:mb-10">
                <Outlet />
            </main>

            {/* <PageFooter /> */}
        </div>
    );
}
