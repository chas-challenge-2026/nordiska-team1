import { useTranslation } from "react-i18next";

type CollapsibleFormBtnsProps = {
    onClose: () => void;
};

export function CollapsibleFormBtns({
    onClose,
}: CollapsibleFormBtnsProps) {

    const {t} = useTranslation();

    return (
        <div className="flex justify-end gap-6 pt-2 font-semibold ">
            <button
                type="button"
                onClick={onClose}
                className="text-error uppercase cursor-pointer"
            >
                {t("generic.cancel")}
            </button>

            <button
                type="submit"
                className="text-success uppercase cursor-pointer"
            >
                {t("generic.save")}
            </button>
        </div>
    );
};