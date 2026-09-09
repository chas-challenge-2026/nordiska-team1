import type { ReactNode } from "react";

type ModalProps = {
    onClose: () => void;
    children: ReactNode;
    widthClassName?: string;
    maxHeightClassName?: string;
};

/**
 * Generiskt modal-skal: overlay + centrerat kort. Klick på overlayn stänger,
 * klick i kortet gör det inte (stoppar propagering). Innehållet avgör själv
 * sin egen padding/scroll — skalet lägger sig inte i det.
 */
export default function Modal({
    onClose,
    children,
    widthClassName = "w-[560px]",
    maxHeightClassName = "max-h-[calc(100vh-5rem)]",
}: ModalProps) {
    return (
        <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-dark-navy/45 p-10"
            onClick={onClose}
        >
            <div
                className={`animate-rise relative flex ${maxHeightClassName} ${widthClassName} flex-col overflow-hidden rounded-xl bg-white shadow-modal`}
                onClick={(e) => e.stopPropagation()}
            >
                {children}
            </div>
        </div>
    );
}
