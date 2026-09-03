type CollapsibleFormBtnsProps = {
    onClose: () => void;
};

export function CollapsibleFormBtns({
    onClose,
}: CollapsibleFormBtnsProps) {
    return (
        <div className="flex justify-end gap-6 pt-2">
            <button
                type="button"
                onClick={onClose}
                className="font-bold uppercase text-red-600 cursor-pointer"
            >
                Avbryt
            </button>

            <button
                type="submit"
                className="font-bold uppercase text-green-600 cursor-pointer"
            >
                Spara
            </button>
        </div>
    );
};