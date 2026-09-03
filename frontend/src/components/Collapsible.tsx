import type { ReactNode } from "react";

type CollapsibleProps = {
  title: string;
  isOpen: boolean;
  onOpenChange: (open: boolean) => void;
  preview?: ReactNode;
  label?: string;
  children: (close: () => void) => ReactNode;
};

/**
 * Collapsible håller titel + en toggle-länk.
 * Stängd visas `preview`, öppen visas `children`.
 * `label` är texten i toggle-länken — helt valfri, visas inte om
 * den inte skickas in. Den som använder Collapsible bestämmer
 * själv vad texten ska vara (t.ex. ÄNDRA/LÄGG TILL för ett fält,
 * eller inget alls för en FAQ-fråga).
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
  label,
  children,
}: CollapsibleProps) {
  const close = () => onOpenChange(false);
  const toggle = () => onOpenChange(!isOpen);

  return (
    <div className="w-full border-b border-nordiska-orange py-4">
      <h3 className="font-montserrat text-lg font-bold text-dark-navy">
        <button
          type="button"
          onClick={toggle}
          aria-expanded={isOpen}
          className="flex w-full items-center justify-between cursor-pointer"
        >
          <span>{title}</span>
          <span className="flex items-center gap-1 text-sm font-semibold uppercase tracking-wide text-nordiska-blue">
            {label}
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
          </span>
        </button>
      </h3>

      {isOpen ? (
        <div className="mt-4">{children(close)}</div>
      ) : (
        preview && (
          <div className="mt-2 font-montserrat text-secondary">{preview}</div>
        )
      )}
    </div>
  );
}
