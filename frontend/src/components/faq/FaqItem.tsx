import Collapsible from "../Collapsible";
import FaqFeedback from "./FaqFeedback";
import type { Faq } from "../../types/types";
import { useViewFaq } from "../../hooks/useFaqs";
import Markdown from "react-markdown";
import remarkBreaks from "remark-breaks";
import { markdownComponents } from "./markdownComponents";

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
                    <div>
                        <Markdown
                            remarkPlugins={[remarkBreaks]}
                            components={markdownComponents}
                        >
                            {faq.answer ?? ""}
                        </Markdown>
                    </div>
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
