import { useEffect, useRef } from "react";
import type { ReactNode } from "react";

type ModalProps = {
    onClose: () => void;
    title: string;
    children: ReactNode;
    widthClassName?: string;
    maxHeightClassName?: string;
};

const FOCUSABLE_SELECTOR =
    'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * Generiskt modal-skal: overlay + centrerat kort. Klick på overlayn stänger,
 * klick i kortet gör det inte (stoppar propagering). Innehållet avgör själv
 * sin egen padding/scroll — skalet lägger sig inte i det.
 *
 * Tillgänglighet: dialog-roll + aria-label (via `title`, ingen synlig
 * dubblett-rubrik), fokus flyttas in vid öppning och återställs vid
 * stängning, Tab fångas inom modalen så man inte kan tabba ut i sidan
 * bakom, Escape stänger.
 */
export default function Modal({
    onClose,
    title,
    children,
    widthClassName = "w-[560px]",
    maxHeightClassName = "max-h-[calc(100vh-5rem)]",
}: ModalProps) {
    const cardRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const previouslyFocused = document.activeElement as HTMLElement | null;
        const card = cardRef.current;
        const focusable = card
            ? Array.from(card.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR))
            : [];
        (focusable[0] ?? card)?.focus();

        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === "Escape") {
                e.preventDefault();
                onClose();
                return;
            }
            if (e.key !== "Tab" || !card) return;
            const items = Array.from(
                card.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
            );
            if (items.length === 0) return;
            const first = items[0];
            const last = items[items.length - 1];
            if (e.shiftKey && document.activeElement === first) {
                e.preventDefault();
                last.focus();
            } else if (!e.shiftKey && document.activeElement === last) {
                e.preventDefault();
                first.focus();
            }
        };

        document.addEventListener("keydown", handleKeyDown);
        return () => {
            document.removeEventListener("keydown", handleKeyDown);
            previouslyFocused?.focus();
        };
        // Ska bara köras en gång per modal-öppning (mount/unmount), inte om
        // föräldern råkar skicka en ny onClose-referens vid omrendering.
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    return (
        <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-dark-navy/45 p-10"
            onClick={onClose}
        >
            <div
                ref={cardRef}
                role="dialog"
                aria-modal="true"
                aria-label={title}
                tabIndex={-1}
                className={`animate-rise relative flex ${maxHeightClassName} ${widthClassName} flex-col overflow-hidden rounded-xl bg-white shadow-modal outline-none`}
                onClick={(e) => e.stopPropagation()}
            >
                {children}
            </div>
        </div>
    );
}
