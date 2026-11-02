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

export default function AdminStatus({ kind, children, className = "" }: Props) {
    const text = children ?? defaults[kind];

    if (kind === "error") {
        return (
            <p role="alert" className={`text-sm text-error ${className}`.trim()}>
                {text}
            </p>
        );
    }

    if (kind === "loading") {
        return (
            <p role="status" className={`text-sm ${className}`.trim()}>
                {text}
            </p>
        );
    }

    return <p className={`text-sm ${className}`.trim()}>{text}</p>;
}
