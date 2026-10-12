import { Outlet } from "react-router";
import PageNavigation from "../components/PageNavigation";
import { useLocation } from "react-router";
import { AnimatePresence, motion } from "motion/react";

export default function DesktopLayout() {
    const location =  useLocation();

    return (
        <div className="flex flex-1 flex-col md:bg-light-gray/40">
            <PageNavigation />
            <main className="flex flex-1 min-h-0 w-full overflow-x-hidden mb-20 xl:mb-0">
                <AnimatePresence mode="wait">
                    <motion.div
                        key={location.pathname}
                        initial={{ opacity: 0 }}
                        animate={{ opacity: 1 }}
                        exit={{ opacity: 0 }}
                        transition={{ duration: 0.2 }}
                        className="w-full min-w-0 pt-[80px] md:pt-[125px]"
                    >
                        <Outlet />
                    </motion.div>
                </AnimatePresence>
            </main>
            {/* <PageFooter /> */}
        </div>
    );
}