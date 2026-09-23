import type { KeyboardEvent, ReactNode } from "react";
import { Reorder, useDragControls } from "motion/react";
import { useTranslation } from "react-i18next";
import type { CardId } from "./useCardOrder";

type ReorderableCardProps = {
    id: CardId;
    label: string;
    isEditing: boolean;
    onKeyboardMove: (id: CardId, delta: -1 | 1) => void;
    children: ReactNode;
};

const KEY_DELTA: Partial<Record<string, -1 | 1>> = {
    ArrowUp: -1,
    ArrowLeft: -1,
    ArrowDown: 1,
    ArrowRight: 1,
};

/**
 * Wraps overview card in Reorder.Item. Drag only via handle (visible in edit mode).
 * Keyboard: focus handle, arrow keys move card back/forward in order.
 */
export default function ReorderableCard({ id, label, isEditing, onKeyboardMove, children }: ReorderableCardProps) {
    const { t } = useTranslation();
    const controls = useDragControls();

    function handleKeyDown(e: KeyboardEvent<HTMLButtonElement>) {
        const delta = KEY_DELTA[e.key];
        if (!delta) return;

        e.preventDefault();
        const handle = e.currentTarget;
        onKeyboardMove(id, delta);
        // DOM node moves on reorder; restore focus so user can keep moving.
        requestAnimationFrame(() => handle.focus());
    }

    return (
        <Reorder.Item
            value={id}
            dragListener={false}
            dragControls={controls}
            className={`relative h-full min-w-0 list-none rounded-xl ${isEditing ? "ring-2 ring-light-blue-accent ring-offset-2" : ""}`}
        >
            {isEditing && (
                <button
                    type="button"
                    onPointerDown={(e) => controls.start(e)}
                    onKeyDown={handleKeyDown}
                    aria-label={t("overview-route.edit-handle-label", { card: label })}
                    aria-describedby="overview-edit-instructions"
                    className="absolute -top-3.5 left-1/2 z-10 flex h-7 w-12 -translate-x-1/2 cursor-grab touch-none items-center justify-center rounded-full border border-light-gray bg-white text-secondary shadow-sm hover:bg-gray-100 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-blue active:cursor-grabbing"
                >
                    <svg width="18" height="12" viewBox="0 0 18 12" fill="currentColor" aria-hidden="true">
                        <circle cx="3" cy="3" r="1.5" />
                        <circle cx="9" cy="3" r="1.5" />
                        <circle cx="15" cy="3" r="1.5" />
                        <circle cx="3" cy="9" r="1.5" />
                        <circle cx="9" cy="9" r="1.5" />
                        <circle cx="15" cy="9" r="1.5" />
                    </svg>
                </button>
            )}
            {children}
        </Reorder.Item>
    );
}
