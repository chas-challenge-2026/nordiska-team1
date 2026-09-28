type StatusTextProps = {
    text: string;
    isError?: boolean;
};

export default function StatusText({ text, isError = false }: StatusTextProps) {
    return (
        <p
            role={isError ? "alert" : "status"}
            className={`text-sm ${isError ? "text-red-700" : "text-secondary"}`}
        >
            {text}
        </p>
    );
}
