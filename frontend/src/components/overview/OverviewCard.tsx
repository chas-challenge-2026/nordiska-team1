import type { ReactNode } from "react";

/**
 * Card container for overview page. Header comes from `Table` inside.
 * `overflow-x-clip` clips horizontal spill without creating a scroll container.
 */
export default function OverviewCard({ children }: { children: ReactNode }) {
    return (
        <section className="flex h-full min-w-0 flex-col overflow-x-clip rounded-xl border border-light-gray bg-white p-4 font-montserrat sm:p-6">
            {children}
        </section>
    );
}
