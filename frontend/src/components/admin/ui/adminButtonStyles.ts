export type AdminButtonVariant = "green" | "yellow" | "red" | "neutral" | "navActive" | "navDanger";

// Literal class strings so Tailwind can detect them at build time.
// Each variant owns its rest and hover colors; the base has none.
const variants: Record<AdminButtonVariant, string> = {
    green: "bg-success text-black border-black not-disabled:hover:bg-white",
    yellow: "bg-nordiska-orange text-black border-black not-disabled:hover:bg-white",
    red: "bg-error text-white border-black not-disabled:hover:bg-white not-disabled:hover:text-black",
    neutral: "bg-white text-black border-black not-disabled:hover:bg-light-gray",
    navActive:
        "bg-black text-white border-black not-disabled:hover:bg-white not-disabled:hover:text-black shadow-[inset_4px_0_0_var(--color-nordiska-orange)]! translate-0!",
    navDanger: "bg-white text-error border-error not-disabled:hover:bg-error not-disabled:hover:text-white",
};

const base =
    "inline-flex items-center justify-center min-h-10 px-4 text-[15px] font-semibold border-2 rounded-sm shadow-[3px_3px_0_#000] transition-[background-color,color,box-shadow,translate] duration-100 not-disabled:hover:translate-px not-disabled:hover:shadow-[2px_2px_0_#000] not-disabled:active:translate-[3px] not-disabled:active:shadow-none focus-visible:outline-3 focus-visible:outline-offset-0 focus-visible:outline-nordiska-orange disabled:bg-light-gray disabled:text-gray-600 disabled:border-gray-400 disabled:shadow-none disabled:cursor-not-allowed motion-reduce:transition-none";

export function adminButtonClass(variant: AdminButtonVariant = "neutral", extra = "") {
    return `${base} ${variants[variant]} ${extra}`.trim();
}
