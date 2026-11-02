import Collapsible from "../Collapsible";
import FaqFeedback from "./FaqFeedback";
import type { Faq } from "../../types/types";
import { useViewFaq } from "../../hooks/useFaqs";

type FeedbackAction = "increase" | "decrease";

type FaqItemProps = {
    faq: Faq;
    isOpen: boolean;
    feedback?: FeedbackAction;
    onOpenChange: (open: boolean) => void;
    onFeedback: (
        action: FeedbackAction
    ) => void;
};

const FaqItem = ({
    faq,
    isOpen,
    feedback,
    onOpenChange,
    onFeedback,
}: FaqItemProps) => {
    const { mutate } = useViewFaq();

    const handleOpenChange = (open: boolean) => {
        if (open) mutate(faq.id);
        onOpenChange(open);
    };

    return (
        <Collapsible
            title={faq.question ?? ""}
            isOpen={isOpen}
            onOpenChange={handleOpenChange}
        >
            {() => (
                <>
                    <p className="whitespace-pre-line">{faq.answer}</p>
                    <FaqFeedback
                        value={feedback}
                        onChange={onFeedback}
                    />
                </>
            )}
        </Collapsible>
    );
};

export default FaqItem;
