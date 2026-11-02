import type { ReactNode } from "react";

type Props = {
    children: ReactNode;
    className?: string;
};

export default function AdminCard({ children, className = "" }: Props) {
    return <div className={`bg-white border-2 border-black rounded-sm p-5 ${className}`.trim()}>{children}</div>;
}
