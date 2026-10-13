type AccountTriggerButtonProps = {
    label: string;
    name: string;
    meta: string;
    balance?: string;
    onClick: () => void;
};

export default function AccountTriggerButton({
    label,
    name,
    meta,
    balance,
    onClick,
}: AccountTriggerButtonProps) {
    return (
        <button
            type="button"
            onClick={onClick}
            className="flex min-h-18 w-full min-w-0 cursor-pointer items-center justify-between gap-3 border border-primary-blue/40 bg-primary-blue/10 px-3 py-2.5 text-left hover:bg-primary-blue/20 sm:px-4 sm:py-3"
        >
            <span className="min-w-0">
                <span className="block truncate text-[10px] font-medium uppercase tracking-wider text-nordiska-blue sm:text-xs">
                    {label}
                </span>
                <span className="mt-0.5 block truncate text-sm font-bold text-dark-navy sm:text-base">
                    {name}
                </span>
                <span className="mt-0.5 block truncate text-xs text-dark-navy/70">
                    {meta}
                    {balance && (
                        <>
                            {" "}• <span className="font-semibold">{balance}</span>
                        </>
                    )}
                </span>
            </span>

            {/* CHEVRON */}
            <span aria-hidden="true" className="mr-1 h-3 w-3 shrink-0 -rotate-45 border-r-2 border-b-2 border-dark-navy" />
        </button>
    );
}
