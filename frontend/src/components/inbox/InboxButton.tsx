import type { ButtonHTMLAttributes } from "react";

const styles = {
    primary: "rounded bg-primary-blue px-4 py-2 font-semibold text-white",
    outline: "rounded border border-primary-blue px-3 py-1 text-xs font-semibold text-primary-blue",
    "outline-light": "rounded border border-white px-3 py-1 text-xs font-semibold text-white",
    link: "text-primary-blue underline",
};

type Props = ButtonHTMLAttributes<HTMLButtonElement> & {
    variant?: keyof typeof styles;
};

export default function InboxButton({ variant = "primary", className = "", ...props }: Props) {
    return (
        <button
            type="button"
            {...props}
            className={`cursor-pointer disabled:cursor-default disabled:opacity-40 ${styles[variant]} ${className}`}
        />
    );
}
