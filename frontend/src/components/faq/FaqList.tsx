import FaqItem from "./FaqItem";
import type { Faq } from "../../types/types";
import { useTranslation } from "react-i18next";

type FeedbackAction = "increase" | "decrease";

type FaqListProps = {
    faqs: Faq[];
    isLoading: boolean;
    isError: boolean;
    openFaqId: number | null;
    selectedFeedback: Record<
        number,
        FeedbackAction
    >;
    onOpenChange: (
        id: number,
        open: boolean
    ) => void;
    onFeedback: (
        id: number,
        action: FeedbackAction
    ) => void;
};

const FaqList = ({
    faqs,
    isLoading,
    isError,
    openFaqId,
    selectedFeedback,
    onOpenChange,
    onFeedback,
}: FaqListProps) => {

    const {t} = useTranslation();

    if (isLoading) {
        return (
            <p> {t("faq.faq-list.loading")} </p>
        );
    }

    if (isError) {
        return (
            <p> {t("faq.faq-list.error")} </p>
        );
    }

    if (faqs.length === 0) {
        return (
            <p> {t("faq.faq-list.no-results")} </p>
        );
    }

    return (
        <div className="flex flex-col gap-3">
            {faqs.map((faq) => (
                <FaqItem
                    key={faq.id}
                    faq={faq}
                    isOpen={openFaqId === faq.id}
                    feedback={selectedFeedback[faq.id]}
                    onOpenChange={(open) =>
                        onOpenChange(faq.id, open)
                    }
                    onFeedback={(action) =>
                        onFeedback(faq.id, action)
                    }
                />
            ))}
        </div>
    );
};

export default FaqList;