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
        <div className="relative mt-5 flex items-center pb-6 sm:mt-6 bg-white">
            <span className="h-0 flex-1 border-b-2 border-light-blue-accent" />
            <button
                type="button"
                aria-label="Flytta sektion"
                onPointerDown={(event) => {
                    event.preventDefault();
                    event.stopPropagation();
                    dragControls.start(event.nativeEvent);
                }}
                className="mx-2 flex shrink-0 items-center cursor-grab touch-none active:cursor-grabbing bg-white"
            >
                <span className="box-content rounded-md border-2 border-primary-blue px-1">
                    <span aria-hidden="true" className="block h-4 w-4 bg-primary-blue/80 mask-[url('/icons/grip.svg')] mask-contain mask-center mask-no-repeat  sm:h-4 sm:w-4" />
                </span>
            </button>
            <span className="h-0 flex-1 border-b-2 border-light-blue-accent" />
        </div>
            {children}
        </Reorder.Item>
    );
}