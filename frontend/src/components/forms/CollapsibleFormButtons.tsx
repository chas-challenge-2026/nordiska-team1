import { useTranslation } from "react-i18next";

type CollapsibleFormBtnsProps = {
    onClose: () => void;
    isSubmitting?: boolean;
    submitDisabled?: boolean;
    submitLabel?: string;
};

export function CollapsibleFormBtns({
    onClose,
    isSubmitting = false,
    submitDisabled = false,
    submitLabel,
}: CollapsibleFormBtnsProps) {

    const {t} = useTranslation();

    return (
        <div className="flex justify-end gap-6 pt-2 font-semibold ">
            <button
                type="button"
                onClick={onClose}
                disabled={isSubmitting}
                className="text-error uppercase cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
            >
                {t("generic.cancel")}
            </button>

            <button
                type="submit"
                disabled={isSubmitting || submitDisabled}
                className="text-success uppercase cursor-pointer disabled:cursor-not-allowed disabled:opacity-50"
            >
                {submitLabel ?? t("generic.save")}
            </button>
        </div>
    );
};
