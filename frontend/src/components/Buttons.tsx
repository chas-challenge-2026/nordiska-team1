import { useTranslation } from "react-i18next";

type CollapsibleFormBtnsProps = {
    onClose: () => void;
};

export function CollapsibleFormBtns({
    onClose,
}: CollapsibleFormBtnsProps) {

    const {t} = useTranslation();

    return (
        <div className="flex justify-end gap-6 pt-2">
            <button
                type="button"
                onClick={onClose}
                className="font-bold uppercase text-red-600 cursor-pointer"
            >
                {t("generic.cancel")}
            </button>

            <button
                type="submit"
                className="font-bold uppercase text-green-600 cursor-pointer"
            >
                {t("generic.save")}
            </button>
        </div>
    );
};