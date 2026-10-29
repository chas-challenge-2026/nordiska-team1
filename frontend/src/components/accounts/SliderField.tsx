import { useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

type SliderFieldProps = {
    label: string;
    value: number;
    min: number;
    max: number;
    step: number;
    onChange: (value: number) => void;
    formatValue: (value: number) => string;
    /** Enhet bredvid fältet när värdet skrivs in manuellt, t.ex. "kr" */
    unit: string;
    /**
     * Manuellt inskrivna värden: "free" tillåter valfritt värde med upp till två decimaler, även över sliderns max
     * (upp till inputMax). "step" avrundar till sliderns steg och håller värdet inom min/max.
     */
    manualEntry?: "free" | "step";
    inputMax?: number;
};

/** Slider med etikett och värde, plus en penna för att skriva in ett exakt värde */
export default function SliderField({ label, value, min, max, step, onChange, formatValue, unit, manualEntry = "step", inputMax = max }: SliderFieldProps) {
    const { t } = useTranslation();
    const id = useId();
    const [draft, setDraft] = useState<string | null>(null);
    const isEditing = draft !== null;
    const inputRef = useRef<HTMLInputElement>(null);

    // Markera nuvarande värde när fältet öppnas, så att man kan skriva över det direkt
    useEffect(() => {
        if (isEditing) inputRef.current?.select();
    }, [isEditing]);

    function commit() {
        if (draft === null) return;
        const parsed = Number(draft.replace(/\s/g, "").replace(",", "."));
        if (draft.trim() !== "" && Number.isFinite(parsed)) {
            const rounded = manualEntry === "free"
                ? Math.round(parsed * 100) / 100
                : Math.round(parsed / step) * step;
            onChange(Math.min(inputMax, Math.max(min, rounded)));
        }
        setDraft(null);
    }

    return (
        <div>
            <div className="flex items-center justify-between gap-3">
                <label htmlFor={isEditing ? `${id}-input` : id} className="text-sm text-dark-navy sm:text-base">{label}</label>
                {isEditing ? (
                    <div className="flex items-center gap-1">
                        <input
                            ref={inputRef}
                            id={`${id}-input`}
                            type="text"
                            inputMode="decimal"
                            value={draft}
                            autoFocus
                            onChange={(e) => setDraft(e.target.value)}
                            onBlur={commit}
                            onKeyDown={(e) => {
                                if (e.key === "Enter") commit();
                                if (e.key === "Escape") setDraft(null);
                            }}
                            className="w-28 rounded-md border border-nordiska-blue bg-white px-2 py-0.5 text-right text-sm font-bold text-dark-navy sm:text-base"
                        />
                        <span className="text-sm text-secondary">{unit}</span>
                    </div>
                ) : (
                    <span className="text-sm font-bold text-dark-navy sm:text-base">{formatValue(value)}</span>
                )}
            </div>

            <div className="mt-1 flex items-center gap-3">
                <input
                    id={id}
                    type="range"
                    min={min}
                    max={max}
                    step={step}
                    value={value}
                    onChange={(e) => onChange(Number(e.target.value))}
                    aria-valuetext={formatValue(value)}
                    className="w-full cursor-pointer accent-primary-blue"
                />
                <button
                    type="button"
                    onClick={() => setDraft(String(value).replace(".", ","))}
                    aria-label={t("accounts-route.forecast-section.edit", { label })}
                    className="flex h-8 w-8 shrink-0 cursor-pointer items-center justify-center rounded-full border border-primary-blue/40 bg-white transition hover:bg-primary-blue-hover"
                >
                    <span aria-hidden="true" className="block h-3.5 w-3.5 bg-primary-blue mask-[url('/icons/edit.svg')] mask-contain mask-center mask-no-repeat" />
                </button>
            </div>
        </div>
    );
}
