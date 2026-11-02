import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Link } from "react-router";

import { adminButtonClass, type AdminButtonVariant } from "./adminButtonStyles";

type Props = {
    variant?: AdminButtonVariant;
    to?: string;
    className?: string;
    children: ReactNode;
} & Omit<ButtonHTMLAttributes<HTMLButtonElement>, "className">;

export default function AdminButton({ variant = "neutral", to, className = "", children, type = "button", ...rest }: Props) {
    const classes = adminButtonClass(variant, className);

    if (to) {
        return (
            <Link to={to} className={classes}>
                {children}
            </Link>
        );
    }

    return (
        <button type={type} className={classes} {...rest}>
            {children}
        </button>
    );
}
