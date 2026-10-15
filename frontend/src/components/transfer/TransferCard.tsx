import type { ReactNode } from "react";

type TransferCardProps = {
    id: string;
    label: string;
    title: string;
    children: ReactNode;
    className?: string;
};

/** Kort med mörkblå header, samma stil som korten på kontosidan. */
export default function TransferCard({ id, label, title, className, children }: TransferCardProps) {
    return (
        <section
            aria-labelledby={id}
            className={`flex min-h-0 min-w-0 flex-col overflow-hidden bg-white md:border md:border-gray-200 md:shadow-sm ${className ?? ""}`}
        >
            {/* CARD HEADER */}
            <div className="flex min-h-22 items-center bg-dark-navy px-3 py-3 text-white sm:px-6">
                <div className="min-w-0">
                    <p className="truncate text-[10px] font-medium uppercase tracking-wider text-white/90 sm:text-xs">{label}</p>
                    <h2 id={id} className="mt-0.5 truncate text-base font-bold tracking-wide sm:mt-1 sm:text-lg">{title}</h2>
                </div>
            </div>

            <div className="flex min-h-0 flex-1 flex-col">{children}</div>
        </section>
    );
}
