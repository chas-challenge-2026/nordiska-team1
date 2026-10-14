import { Reorder, useDragControls } from "motion/react";
import { type SectionId } from "../../pages/AccountsPage";

type DraggableSectionProps = {
    id: SectionId;
    children: React.ReactNode;
};

export default function DraggableSection({id,children,}: DraggableSectionProps) {
    const dragControls = useDragControls();

    return (
        <Reorder.Item
            value={id}
            drag="y"
            dragListener={false}
            dragControls={dragControls}
            className="relative"
        >
            <button
                type="button"
                aria-label="Flytta sektion"
                onPointerDown={(event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    dragControls.start(event.nativeEvent);
                }}
                className="flex w-full mt-4 pb-3 sm:mt-6 sm:pb-3 items-center cursor-grab touch-none active:cursor-grabbing bg-white"
            >
                <span className="h-0 flex-1 border-b-2 border-nordiska-orange" />
                <span className="box-content rounded-md border border-nordiska-orange px-1">
                    <span aria-hidden="true" className="block h-4 w-4 bg-primary-blue/80 mask-[url('/icons/grip.svg')] mask-contain mask-center mask-no-repeat  sm:h-4 sm:w-4" />
                </span>
                <span className="h-0 flex-1 border-b-2 border-nordiska-orange" />
            </button>
            {children}
        </Reorder.Item>
    );
}