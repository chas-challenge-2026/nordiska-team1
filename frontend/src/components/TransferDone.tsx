import { useTranslation } from "react-i18next";

export type TransferPhase = "processing" | "success" | "failure";

type TransferDoneProps = {
    phase: TransferPhase;
    summaryLine: string;
    fromName: string;
    toName: string;
    onReset: () => void;
};

/**
 * Bekräftelsevy efter en överföring. Tre faser:
 * - "processing": simulerar att överföringen skickas (resande punkt mellan
 *   kontona) — inget resultat visas än.
 * - "success" / "failure": bearbetningsvyn tonas bort och resultatet tonas
 *   in, så de aldrig visas samtidigt.
 */
export default function TransferDone({
    phase,
    summaryLine,
    fromName,
    toName,
    onReset,
}: TransferDoneProps) {
    const { t } = useTranslation();
    const isProcessing = phase === "processing";
    const isFailure = phase === "failure";

    return (
        <div className="relative py-2 pb-6">
            <div
                className={`transition-opacity duration-300 ${
                    isProcessing
                        ? "opacity-100"
                        : "pointer-events-none absolute inset-0 opacity-0"
                }`}
            >
                <p className="m-0 text-sm font-semibold text-secondary">
                    {t("page-transfer.done.processing")}
                </p>
                <div className="mt-6 flex max-w-[520px] items-center gap-4">
                    <div className="flex-1 rounded-lg border border-[#E5EAF0] bg-white px-4 py-3.5">
                        <div className="text-xs tracking-[0.08em] text-secondary uppercase">
                            {t("generic.from")}
                        </div>
                        <div className="text-[15px] font-bold text-dark-navy">
                            {fromName}
                        </div>
                    </div>
                    <div className="relative h-0.5 w-28 flex-none bg-[#E5EAF0]">
                        <div className="absolute -top-1 left-0 h-2.5 w-2.5 animate-[dot-travel_1.8s_ease-in-out_infinite] rounded-full bg-nordiska-orange" />
                    </div>
                    <div className="flex-1 rounded-lg border border-[#E5EAF0] bg-white px-4 py-3.5">
                        <div className="text-xs tracking-[0.08em] text-secondary uppercase">
                            {t("generic.to")}
                        </div>
                        <div className="text-[15px] font-bold text-dark-navy">
                            {toName}
                        </div>
                    </div>
                </div>
            </div>

            {!isProcessing && (
                <div className="animate-rise">
                    <div className="flex items-center gap-4">
                        <svg
                            width="56"
                            height="56"
                            viewBox="0 0 76 76"
                            fill="none"
                            aria-hidden="true"
                        >
                            <circle
                                cx="38"
                                cy="38"
                                r="34"
                                stroke="var(--color-nordiska-orange)"
                                strokeWidth="3"
                                strokeDasharray="214"
                                className="animate-[ring-draw_0.7s_ease-out_both]"
                            />
                            {isFailure ? (
                                <path
                                    d="M27 27 L49 49 M49 27 L27 49"
                                    stroke="#C4291C"
                                    strokeWidth="4"
                                    strokeLinecap="round"
                                    strokeDasharray="64"
                                    className="animate-[x-draw_0.45s_0.55s_ease-out_both]"
                                />
                            ) : (
                                <path
                                    d="M23 39.5 L34 50 L54 28"
                                    stroke="var(--color-nordiska-blue)"
                                    strokeWidth="4"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                    strokeDasharray="44"
                                    className="animate-[check-draw_0.45s_0.55s_ease-out_both]"
                                />
                            )}
                        </svg>
                        <div>
                            <h2 className="m-0 text-2xl font-semibold text-dark-navy">
                                {t(
                                    isFailure
                                        ? "page-transfer.done.failure-heading"
                                        : "page-transfer.done.heading",
                                )}
                            </h2>
                            {!isFailure && (
                                <p className="mt-1.5 text-sm text-secondary">
                                    {summaryLine}
                                </p>
                            )}
                        </div>
                    </div>

                    <div className="mt-8 flex max-w-[520px] items-center gap-4">
                        <div className="flex-1 rounded-lg border border-[#E5EAF0] bg-white px-4 py-3.5">
                            <div className="text-xs tracking-[0.08em] text-secondary uppercase">
                                {t("generic.from")}
                            </div>
                            <div className="text-[15px] font-bold text-dark-navy">
                                {fromName}
                            </div>
                        </div>
                        <div className="h-0.5 w-28 flex-none bg-[#E5EAF0]" />
                        <div className="flex-1 rounded-lg border border-[#E5EAF0] bg-white px-4 py-3.5">
                            <div className="text-xs tracking-[0.08em] text-secondary uppercase">
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
                        className="mt-9 cursor-pointer rounded-md border-0 bg-nordiska-blue px-6 py-3 text-sm font-bold text-white hover:bg-login-bg"
                    >
                        {t(
                            isFailure
                                ? "page-transfer.done.back-to-transfers"
                                : "page-transfer.done.new-transfer",
                        )}
                    </button>
                </div>
            )}
        </div>
    );
}
