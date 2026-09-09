import { useTranslation } from "react-i18next";

type TransferDoneProps = {
    summaryLine: string;
    fromName: string;
    toName: string;
    onReset: () => void;
};

/**
 * Lugn bekräftelsevy efter genomförd överföring. Ersätter formuläret i
 * vänsterkolumnen. Ingen konfetti, ingen gamification.
 */
export default function TransferDone({
    summaryLine,
    fromName,
    toName,
    onReset,
}: TransferDoneProps) {
    const { t } = useTranslation();

    return (
        <div className="animate-rise py-2 pb-6">
            <div className="flex items-center gap-4">
                <svg width="56" height="56" viewBox="0 0 76 76" fill="none">
                    <circle
                        cx="38"
                        cy="38"
                        r="34"
                        stroke="var(--color-nordiska-orange)"
                        strokeWidth="3"
                        strokeDasharray="214"
                        className="animate-ring-draw"
                    />
                    <path
                        d="M23 39.5 L34 50 L54 28"
                        stroke="var(--color-nordiska-blue)"
                        strokeWidth="4"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                        strokeDasharray="44"
                        className="animate-check-draw"
                    />
                </svg>
                <div>
                    <h2 className="m-0 text-2xl font-semibold text-dark-navy">
                        {t("page-transfer.done.heading")}
                    </h2>
                    <p className="mt-1.5 text-sm text-secondary">
                        {summaryLine}
                    </p>
                </div>
            </div>

            <div className="mt-8 flex max-w-[520px] items-center gap-4">
                <div className="flex-1 rounded-lg border border-border-light bg-white px-4 py-3.5">
                    <div className="text-xs tracking-[0.08em] text-meta uppercase">
                        {t("generic.from")}
                    </div>
                    <div className="text-[15px] font-bold text-dark-navy">
                        {fromName}
                    </div>
                </div>
                <div className="relative h-0.5 w-28 flex-none bg-border-light">
                    <div className="animate-dot-travel absolute -top-1 left-0 h-2.5 w-2.5 rounded-full bg-nordiska-orange" />
                </div>
                <div className="flex-1 rounded-lg border border-border-light bg-white px-4 py-3.5">
                    <div className="text-xs tracking-[0.08em] text-meta uppercase">
                        {t("generic.to")}
                    </div>
                    <div className="text-[15px] font-bold text-dark-navy">
                        {toName}
                    </div>
                </div>
            </div>

            <button
                type="button"
                onClick={onReset}
                className="mt-9 cursor-pointer rounded-md border border-nordiska-blue bg-white px-6 py-3 text-sm font-bold text-nordiska-blue hover:bg-hover-bg"
            >
                {t("page-transfer.done.new-transfer")}
            </button>
        </div>
    );
}
