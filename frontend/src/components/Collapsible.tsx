import type { ReactNode } from "react";

type CollapsibleProps = {
    title: string;
    isOpen: boolean;
    onOpenChange: (open: boolean) => void;
    preview?: ReactNode;
    children: (close: () => void) => ReactNode;
};

/**
 * Collapsible håller titel + en toggle-länk.
 * Stängd visas `preview`, öppen visas `children`.
 * Om `preview` finns (dvs värdet är redan registrerat) blir länken
 * "ÄNDRA", annars "LÄGG TILL" — ingen som använder komponenten
 * behöver ange det manuellt.
 * `children` får en `close`-funktion så att t.ex. ett formulär kan
 * stänga collapsible vid avbryt/spara utan att denna komponent
 * behöver veta något om formulärets innehåll.
 *
 * Öppet/stängt styrs utifrån (isOpen/onOpenChange) istället för att
 * Collapsible äger sitt eget state — det gör det möjligt för föräldern
 * att bara hålla en collapsible öppen åt gången (accordion).
 */
export default function Collapsible({
    title,
    isOpen,
    onOpenChange,
    preview,
    children,
}: CollapsibleProps) {
    const trigger = preview ? "ÄNDRA" : "LÄGG TILL";

    const close = () => onOpenChange(false);
    const toggle = () => onOpenChange(!isOpen);

    return (
        <div className="w-full border-b border-nordiska-orange py-4">
            <div className="flex items-center justify-between">
                <h3 className="font-montserrat text-lg font-bold text-dark-navy">
                    {title}
                </h3>

                <button
                    type="button"
                    onClick={toggle}
                    aria-expanded={isOpen}
                    className="flex items-center gap-1 font-montserrat text-sm font-semibold uppercase tracking-wide text-nordiska-blue"
                >
                    {trigger}
                    <svg
                        className={`h-3 w-3 transition-transform duration-200 ${
                            isOpen ? "rotate-180" : ""
                        }`}
                        viewBox="0 0 12 8"
                        fill="none"
                    >
                        <path
                            d="M1 1L6 6L11 1"
                            stroke="currentColor"
                            strokeWidth="2"
                            strokeLinecap="round"
                            strokeLinejoin="round"
                        />
                    </svg>
                </button>
            </div>

            {isOpen ? (
                <div className="mt-4">{children(close)}</div>
            ) : (
                preview && (
                    <div className="mt-2 font-montserrat text-secondary">
                        {preview}
                    </div>
                )
            )}
        </div>
    );
}
