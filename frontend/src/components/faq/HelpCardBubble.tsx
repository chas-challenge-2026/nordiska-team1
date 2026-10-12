import { useRef, useState } from "react";
import type { ReactNode, PointerEvent as ReactPointerEvent } from "react";
import HelpCard from "./HelpCard";
import Modal from "../modals/Modal";
import { useHelpCardBubblePosition } from "../../hooks/useHelpCardBubblePosition";
import { useTranslation } from "react-i18next";

/**
 * HelpCardBubble
 *
 * Mobile/tablet version of the HelpCard.
 *
 * Displays a draggable floating help button that opens the reusable
 * HelpCard inside a modal when clicked.
 *
 * The bubble can be moved around the screen by dragging it.
 * Clicking the bubble without dragging opens the HelpCard modal.
 *
 * Props:
 * - items: Object containing items with a title, answer, and optional action.
 * - searchTerms: Optional search terms used to fetch relevant FAQs.
 * - numOfHits: Default 5. Determines how many FAQ matches are returned.
 * - removeHeading: Pass to remove the HelpCard heading.
 * - heading: Optional heading used for the HelpCard and modal title.
 *
 * ---------------------------------------------------
 * ------------------- USAGE -------------------------
 * ---------------------------------------------------
 *
 * Place HelpCardBubble inside the page where the floating help button
 * should be available:
 *
 * <HelpCardBubble
 *     items={helpfulArticles}
 * />
 *
 * ---------------------------------------------------
 * -------- USING HELPCARD WITH RELEVANT FAQS --------
 * ---------------------------------------------------
 *
 * Pass searchTerms to fetch relevant FAQs from the API:
 *
 * <HelpCardBubble
 *     searchTerms={t("transaction-page.help-card-searchterms")}
 * />
 *
 * ---------------------------------------------------
 * ------- USING HELPCARD WITH A CUSTOM OBJECT -------
 * ---------------------------------------------------
 *
 * Pass a custom object containing the items to display:
 *
 * const items = {
 *     changeLanguage: {
 *         title: "...",
 *         answer: "...",
 *         action: <LanguageButton />,
 *     },
 *     updateInfo: {
 *         title: "...",
 *         answer: "...",
 *         action: <Link to="/settings">...</Link>,
 *     },
 * };
 *
 * <HelpCardBubble
 *     items={items}
 * />
 *
 * ---------------------------------------------------
 * -------------------- BEHAVIOR ---------------------
 * ---------------------------------------------------
 *
 * - The help bubble is displayed as a floating circular button.
 * - The bubble can be dragged to another position on the screen.
 * - Clicking without dragging opens the HelpCard in a modal.
 * - The modal can be closed using the modal's close functionality.
 * - The bubble's position is handled by useHelpCardBubblePosition.
 *
 * The component is intended as a responsive alternative to displaying
 * HelpCard directly in a sidebar or other fixed page layout.
 */

type InfoItem = {
    title: string;
    answer: string;
    action?: ReactNode;
};

type HelpCardBubbleProps = {
    heading?: string;
    removeHeading?: boolean;
    items?: Record<string, InfoItem>;
    searchTerms?: string;
    numOfHits?: number;
};

export default function HelpCardBubble({
    heading,
    removeHeading = false,
    items,
    searchTerms,
    numOfHits = 5,
}: HelpCardBubbleProps) {
    const {t} = useTranslation();
    const [isOpen, setIsOpen] = useState(false);
    const [openCount, setOpenCount] = useState(0);
    const { position, updatePosition } = useHelpCardBubblePosition();
    const isDragging = useRef(false);
    const hasMoved = useRef(false);
    const dragOffset = useRef({ x: 0, y: 0 });

    const handlePointerDown = (e: ReactPointerEvent<HTMLButtonElement>) => {
        isDragging.current = true;
        hasMoved.current = false;

        dragOffset.current = {x: e.clientX - position.x, y: e.clientY - position.y,};

        e.currentTarget.setPointerCapture(e.pointerId);
    };

    const handlePointerMove = (e: ReactPointerEvent<HTMLButtonElement>) => {
        if (!isDragging.current) return;

        const newX = e.clientX - dragOffset.current.x;
        const newY = e.clientY - dragOffset.current.y;

        if (Math.abs(newX - position.x) > 3 || Math.abs(newY - position.y) > 3) {hasMoved.current = true;}

        const size = 56;

        updatePosition({
            x: Math.max(0, Math.min(newX, window.innerWidth - size)),
            y: Math.max(0, Math.min(newY, window.innerHeight - size)),
        });
    };

    const handlePointerUp = (e: ReactPointerEvent<HTMLButtonElement>) => {
        isDragging.current = false;
        e.currentTarget.releasePointerCapture(e.pointerId);
    };

    return (
    <>
        {/* BUBBLA */}
        {!isOpen && (
            <button
                type="button"
                aria-label={t("aria-label.open-help")}
                onClick={(e) => {
                    e.stopPropagation();

                    if (!hasMoved.current) {
                        setOpenCount((prev) => prev + 1);
                        setIsOpen(true);
                    }
                }}
                onPointerDown={handlePointerDown}
                onPointerMove={handlePointerMove}
                onPointerUp={handlePointerUp}
                style={{...(position
                        ? {left: position.x, top: position.y,}
                        : {}),
                    touchAction: "none",
                }}
                className="xl:hidden fixed z-[1000] h-14 w-14 rounded-full bg-dark-navy text-white shadow-lg flex items-center justify-center text-xl font-semibold cursor-grab active:cursor-grabbing select-none"
            >
                ?
            </button>
        )}

        {/* ÖPPEN */}
        <Modal
            isOpen={isOpen}
            onClose={() => setIsOpen(false)}
            title={t("generic.help")}
            widthClassName="w-full lg:max-w-[50vw]"
            maxHeightClassName="h-[35vh]"
        >
            <div className="h-full p-4 mt-1">
                <HelpCard
                    key={openCount}
                    heading={heading}
                    removeHeading={removeHeading}
                    items={items}
                    searchTerms={searchTerms}
                    numOfHits={numOfHits}
                />               
            </div>
        </Modal>
    </>
    );
}