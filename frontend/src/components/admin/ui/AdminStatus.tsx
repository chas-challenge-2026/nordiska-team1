type Props = {
    kind: "loading" | "error" | "empty";
    children?: string;
    className?: string;
};

const defaults = {
    loading: "Laddar...",
    error: "Något gick fel.",
    empty: "Inget att visa.",
};

const base = "bg-white border border-gray-300 border-l-4 rounded-sm px-4 py-3 text-[15px]";

export default function AdminStatus({ kind, children, className = "" }: Props) {
    const text = children ?? defaults[kind];

    if (kind === "error") {
        return (
            <p role="alert" className={`${base} border-error text-error font-semibold ${className}`.trim()}>
                {text}
            </p>
        );
    }

    if (kind === "loading") {
        return (
            <p role="status" className={`${base} border-l-dark-navy text-gray-600 ${className}`.trim()}>
                {text}
            </p>
        );
    }

    return <p className={`${base} border-l-gray-400 text-gray-600 ${className}`.trim()}>{text}</p>;
}
