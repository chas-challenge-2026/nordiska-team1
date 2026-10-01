import Collapsible from "../Collapsible";
import FaqFeedback from "./FaqFeedback";
import type { Faq } from "../../types/types";

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
    return (
        <Collapsible
            title={faq.question ?? ""}
            isOpen={isOpen}
            onOpenChange={onOpenChange}
        >
            {() => (
                <>
                    <p>{faq.answer}</p>

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