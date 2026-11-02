import type { ReactNode } from "react";

type Props = {
    children: ReactNode;
    danger?: boolean;
    className?: string;
};

export function AdminPageTitle({ children, danger = false, className = "" }: Props) {
    return (
        <h1 className={`text-[28px] font-bold leading-tight text-left text-balance text-white px-5 py-3.5 border-b-4 border-nordiska-orange ${danger ? "bg-error" : "bg-black"} ${className}`.trim()}>
            {children}
        </h1>
    );
}

export function AdminSectionTitle({ children, className = "" }: Omit<Props, "danger">) {
    return (
        <h2 className={`text-[15px] font-semibold uppercase tracking-[0.08em] text-left px-4 py-2.5 bg-black text-white ${className}`.trim()}>
            {children}
        </h2>
    );
}
