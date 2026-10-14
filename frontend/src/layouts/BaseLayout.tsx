import { Outlet } from "react-router";
import MobileNavigation from "../components/MobileNavigation";
import { useLocation } from "react-router";
import { AnimatePresence, motion } from "motion/react";

export default function DesktopLayout() {
    const location =  useLocation();

    return (
        <div className="flex flex-1 flex-col bg-nordiska-bg">
            <MobileNavigation />
            <main className="flex flex-1 min-h-0 w-full overflow-x-hidden pb-20 xl:mb-0">
                <AnimatePresence mode="wait">
                    <motion.div
                        key={location.pathname.split("/")[1]}
                        initial={{ opacity: 0 }}
                        animate={{ opacity: 1 }}
                        exit={{ opacity: 0 }}
                        transition={{ duration: 0.2 }}
                        className="flex flex-1 min-h-0 w-full"
                    >
                        <Outlet />
                    </motion.div>
                </AnimatePresence>
            </main>
            {/* <PageFooter /> */}
        </div>
    );
}
