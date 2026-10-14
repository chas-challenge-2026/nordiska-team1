import { useTranslation } from "react-i18next";

type FeedbackAction = "increase" | "decrease";

type FaqFeedbackProps = {
    value?: FeedbackAction;
    onChange: (action: FeedbackAction) => void;
};

const FaqFeedback = ({
    value,
    onChange,
}: FaqFeedbackProps) => {
    const hasGivenFeedback = Boolean(value);
    const {t} = useTranslation();

    return (
        <div className="flex justify-end items-center mt-15">
            <p
                id="faq-feedback-question"
                className="text-sm mr-5"
                role="status"
                aria-live="polite"
            >
                {hasGivenFeedback
                    ? t("faq.faq-feedback.feedback")
                    : t("faq.faq-feedback.question")}
            </p>

            <div
                className="flex items-center"
                role="group"
                aria-label={t("faq.faq-feedback.aria-label.div")}
            >
                <button
                    type="button"
                    aria-label={t("faq.faq-feedback.aria-label.btn-yes")}
                    aria-pressed={value === "increase"}
                    title={t("generic.yes")}
                    onClick={() => onChange("increase")}
                    className={`group ${
                        value === "increase"
                            ? "cursor-not-allowed"
                            : "cursor-pointer"
                    } p-1`}
                    disabled={value === "increase"}
                >
                    <img
                        src={
                            value === "increase"
                                ? "/icons/thumbs-up-full.svg"
                                : "/icons/thumbs-up.svg"
                        }
                        alt=""
                        className="h-7 w-7"
                    />
                </button>

                <button
                    type="button"
                    aria-label={t("faq.faq-feedback.aria-label.btn-no")}
                    aria-pressed={value === "decrease"}
                    title={t("generic.no")}
                    onClick={() => onChange("decrease")}
                    className={`group ${
                        value === "decrease"
                            ? "cursor-not-allowed"
                            : "cursor-pointer"
                    } p-1`}
                    disabled={value === "decrease"}
                >
                    <img
                        src={
                            value === "decrease"
                                ? "/icons/thumbs-down-full.svg"
                                : "/icons/thumbs-down.svg"
                        }
                        alt=""
                        className="h-7 w-7 scale-x-[-1]"
                    />
                </button>
            </div>
        </div>
    );
};

export default FaqFeedback;