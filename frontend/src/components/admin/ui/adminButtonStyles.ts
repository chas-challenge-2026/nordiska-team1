export type AdminButtonVariant = "green" | "yellow" | "red" | "neutral";

// Literal class strings so Tailwind can detect them at build time.
const variants: Record<AdminButtonVariant, string> = {
    green: "bg-green-500 hover:border-green-500",
    yellow: "bg-yellow-500 hover:border-yellow-500",
    red: "bg-red-500 hover:border-red-500",
    neutral: "bg-white hover:border-gray-400",
};

const base =
    "inline-block border-5 border-black rounded-md px-4 py-2 text-center font-semibold cursor-pointer hover:bg-white hover:text-black focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-nordiska-orange disabled:cursor-not-allowed disabled:opacity-50";

export function adminButtonClass(variant: AdminButtonVariant = "neutral", extra = "") {
    return `${base} ${variants[variant]} ${extra}`.trim();
}
