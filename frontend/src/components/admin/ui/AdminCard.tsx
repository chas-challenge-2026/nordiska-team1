import type { ReactNode } from "react";

type Props = {
    children: ReactNode;
    className?: string;
};

export default function AdminCard({ children, className = "" }: Props) {
    return <div className={`border rounded-lg p-4 ${className}`.trim()}>{children}</div>;
}
