import type { ReactNode } from "react";

export default function OverviewCard({ children }: { children: ReactNode }) {
    return (
        <section className="min-h-[320px] w-full min-w-0 overflow-x-clip rounded-3xl border border-gray-200 bg-white shadow-md aspect-square sm:min-h-0">
            {children}
        </section>
    );
}
