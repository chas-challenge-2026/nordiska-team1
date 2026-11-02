import type { ReactNode } from "react";

type Props = {
    children: ReactNode;
    danger?: boolean;
    className?: string;
};

export function AdminPageTitle({ children, danger = false, className = "" }: Props) {
    return (
        <h1 className={`text-3xl font-semibold py-2.5 text-white text-center ${danger ? "bg-red-600" : "bg-black"} ${className}`.trim()}>
            {children}
        </h1>
    );
}

export function AdminSectionTitle({ children, className = "" }: Omit<Props, "danger">) {
    return (
        <h2 className={`text-xl font-semibold py-2 bg-black text-white text-center ${className}`.trim()}>
            {children}
        </h2>
    );
}
