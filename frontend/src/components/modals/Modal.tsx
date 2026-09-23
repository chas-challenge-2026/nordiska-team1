import { useEffect, useRef } from "react";
import type { ReactNode } from "react";
import { AnimatePresence, motion } from "motion/react";
import type { PanInfo } from "motion/react";

type ModalProps = {
    isOpen: boolean;
    onClose: () => void;
    onCloseAnimationComplete?: () => void;
    title: string;
    children: ReactNode;
    widthClassName?: string;
    maxHeightClassName?: string;
    closeOnOverlayClick?: boolean;
    closeOnEscape?: boolean;
};

const FOCUSABLE_SELECTOR =
    'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

const DRAG_CLOSE_THRESHOLD_PX = 120;
const DRAG_CLOSE_VELOCITY = 500; // px/s
const DRAG_CLOSE_VISIBLE_RATIO = 0.05; // close once only 5% of the modal remains visible

export default function Modal({
    isOpen,
    onClose,
    onCloseAnimationComplete,
    title,
    children,
    widthClassName = "w-full sm:w-[560px]",
    maxHeightClassName = "max-h-[85vh] sm:max-h-[calc(100vh-5rem)]",
    closeOnOverlayClick = true,
    closeOnEscape = true,
}: ModalProps) {
    const cardRef = useRef<HTMLDivElement>(null);
    const hasClosedRef = useRef(false);

    useEffect(() => {
        if (!isOpen) return;

        const previouslyFocused = document.activeElement as HTMLElement | null;
        const card = cardRef.current;
        const focusable = card
            ? Array.from(card.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR))
            : [];
        (focusable[0] ?? card)?.focus();

        const handleKeyDown = (e: KeyboardEvent) => {
            if (e.key === "Escape" && closeOnEscape) {
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
    }, [isOpen, closeOnEscape, onClose]);

    const handleDragStart = () => {
        hasClosedRef.current = false;
    };

    const handleDrag = (
        _e: MouseEvent | TouchEvent | PointerEvent,
        info: PanInfo,
    ) => {
        if (hasClosedRef.current || !cardRef.current) return;
        const height = cardRef.current.getBoundingClientRect().height;
        if (height > 0 && info.offset.y >= height * (1 - DRAG_CLOSE_VISIBLE_RATIO)) {
            hasClosedRef.current = true;
            onClose();
        }
    };

    const handleDragEnd = (
        _e: MouseEvent | TouchEvent | PointerEvent,
        info: PanInfo,
    ) => {
        if (hasClosedRef.current) return;
        if (
            info.offset.y > DRAG_CLOSE_THRESHOLD_PX ||
            info.velocity.y > DRAG_CLOSE_VELOCITY
        ) {
            hasClosedRef.current = true;
            onClose();
        }
    };

    return (
        <AnimatePresence onExitComplete={onCloseAnimationComplete}>
            {isOpen && (
                <motion.div
                    className="fixed inset-0 z-50 flex items-end justify-center bg-dark-navy/45 sm:items-center sm:p-10"
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    exit={{ opacity: 0 }}
                    onClick={closeOnOverlayClick ? onClose : undefined}
                >
                    <motion.div
                        ref={cardRef}
                        role="dialog"
                        aria-modal="true"
                        aria-label={title}
                        tabIndex={-1}
                        className={`relative flex ${maxHeightClassName} ${widthClassName} flex-col overflow-hidden rounded-t-xl bg-white shadow-modal outline-none sm:rounded-xl`}
                        style={{
                            paddingBottom: "env(safe-area-inset-bottom, 0px)"
                        }}
                        initial={{ y: "100%" }}
                        animate={{ y: 0 }}
                        exit={{ y: "100%" }}
                        transition={{ type: "spring", stiffness: 400, damping: 40 }}
                        drag="y"
                        dragConstraints={{ top: 0, bottom: 0 }}
                        dragElastic={{ top: 0, bottom: 0.5 }}
                        onDragStart={handleDragStart}
                        onDrag={handleDrag}
                        onDragEnd={handleDragEnd}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="flex shrink-0 cursor-grab justify-center py-2 active:cursor-grabbing sm:hidden">
                            <div className="h-1.5 w-10 rounded-full bg-gray-300" />
                        </div>
                        <button
                            type="button"
                            onClick={onClose}
                            aria-label="Close"
                            className="absolute top-3 right-3 z-10 rounded-md p-2 text-gray-400 hover:text-gray-700 hover:bg-gray-100 text-xl leading-none cursor-pointer"
                        >
                            ×
                        </button>
                        {children}
                    </motion.div>
                </motion.div>
            )}
        </AnimatePresence>
    );
}
