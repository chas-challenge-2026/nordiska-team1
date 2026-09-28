import type { ReactNode } from "react";

export default function OverviewCard({ children }: { children: ReactNode }) {
    return (
        <section className="flex h-full min-w-0 flex-col overflow-x-clip rounded-xl border border-secondary bg-white p-4 font-montserrat sm:p-6">
            {children}
        </section>
    );
}
