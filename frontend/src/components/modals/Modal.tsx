import { useLayoutEffect, useRef } from "react";
import type {
    ReactNode,
    MouseEvent as ReactMouseEvent,
    PointerEvent as ReactPointerEvent,
    SyntheticEvent,
} from "react";
import { AnimatePresence, motion, useDragControls } from "motion/react";
import type { PanInfo } from "motion/react";
import { useIsMobile } from "../../hooks/useIsMobile";

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

const DRAG_CLOSE_THRESHOLD_PX = 200;
const DRAG_CLOSE_VELOCITY = 500; // px/s

function useBodyScrollLock(locked: boolean) {
    useLayoutEffect(() => {
        if (!locked) return;
        const { body, documentElement } = document;
        const scrollY = window.scrollY;
        const scrollbarGap = window.innerWidth - documentElement.clientWidth;
        const previous = {
            position: body.style.position,
            top: body.style.top,
            width: body.style.width,
            overflow: body.style.overflow,
            paddingRight: body.style.paddingRight,
        };

        body.style.position = "fixed";
        body.style.top = `-${scrollY}px`;
        body.style.width = "100%";
        body.style.overflow = "hidden";
        if (scrollbarGap > 0) body.style.paddingRight = `${scrollbarGap}px`;

        return () => {
            Object.assign(body.style, previous);
            window.scrollTo({ top: scrollY, behavior: "instant" });
        };
    }, [locked]);
}

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
    const dialogRef = useRef<HTMLDialogElement>(null);
    const previouslyFocusedRef = useRef<HTMLElement | null>(null);
    const wasDraggedRef = useRef(false);
    const pointerDownOnOverlayRef = useRef(false);
    const isMobile = useIsMobile();
    const dragControls = useDragControls();

    useBodyScrollLock(isOpen);

    useLayoutEffect(() => {
        if (!isOpen) return;
        const dialog = dialogRef.current;
        if (!dialog || dialog.open) return;
        previouslyFocusedRef.current = document.activeElement as HTMLElement | null;
        dialog.showModal();
    }, [isOpen]);

    const handleCancel = (e: SyntheticEvent<HTMLDialogElement>) => {
        e.preventDefault();
        if (closeOnEscape) onClose();
    };

    const handleNativeClose = () => {
        onClose();
    };

    const handleOverlayPointerDown = (e: ReactPointerEvent<HTMLDialogElement>) => {
        pointerDownOnOverlayRef.current = e.target === e.currentTarget;
        if (!pointerDownOnOverlayRef.current) return;
        wasDraggedRef.current = false;
        if (isMobile) dragControls.start(e);
    };

    const handleOverlayClick = (e: ReactMouseEvent<HTMLDialogElement>) => {
        const startedOnOverlay = pointerDownOnOverlayRef.current;
        pointerDownOnOverlayRef.current = false;
        if (e.target !== e.currentTarget || !startedOnOverlay || wasDraggedRef.current) return;
        if (closeOnOverlayClick) onClose();
    };

    const handleDragStart = () => {
        wasDraggedRef.current = true;
    };

    const handleDragEnd = (
        _e: MouseEvent | TouchEvent | PointerEvent,
        info: PanInfo,
    ) => {
        if (
            info.offset.y > DRAG_CLOSE_THRESHOLD_PX ||
            info.velocity.y > DRAG_CLOSE_VELOCITY
        ) {
            onClose();
        }
    };

    const handleExitComplete = () => {
        const el = previouslyFocusedRef.current;
        previouslyFocusedRef.current = null;
        if (el?.isConnected) el.focus();
        onCloseAnimationComplete?.();
    };

    return (
        <AnimatePresence onExitComplete={handleExitComplete}>
            {isOpen && (
                <motion.dialog
                    ref={dialogRef}
                    aria-label={title}
                    className="fixed inset-0 m-0 hidden h-full max-h-none w-full max-w-none items-end justify-center overflow-hidden border-0 bg-dark-navy/45 p-0 text-inherit outline-none open:flex backdrop:bg-transparent sm:items-center sm:p-10"
                    style={{ touchAction: "none" }}
                    initial={{ opacity: 0 }}
                    animate={{ opacity: 1 }}
                    exit={{ opacity: 0 }}
                    onCancel={handleCancel}
                    onClose={handleNativeClose}
                    onPointerDown={handleOverlayPointerDown}
                    onClick={handleOverlayClick}
                >
                    <motion.div
                        className={`relative flex ${maxHeightClassName} ${widthClassName} flex-col overflow-hidden rounded-t-xl bg-white shadow-modal sm:rounded-xl`}
                        style={{ paddingBottom: "env(safe-area-inset-bottom, 0px)" }}
                        initial={{ y: "100%" }}
                        animate={{ y: 0 }}
                        exit={{ y: "100%" }}
                        transition={{ type: "spring", stiffness: 400, damping: 40 }}
                        drag={isMobile ? "y" : false}
                        dragControls={dragControls}
                        dragListener={false}
                        dragConstraints={{ top: 0, bottom: 0 }}
                        dragElastic={{ top: 0, bottom: 0.5 }}
                        onDragStart={handleDragStart}
                        onDragEnd={handleDragEnd}
                    >
                        <div
                            className="flex shrink-0 cursor-grab justify-center py-2 active:cursor-grabbing sm:hidden"
                            onPointerDown={(e) => dragControls.start(e)}
                            style={{ touchAction: "none" }}
                        >
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
                </motion.dialog>
            )}
        </AnimatePresence>
    );
}
