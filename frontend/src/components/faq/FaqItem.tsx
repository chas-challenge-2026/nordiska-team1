import Collapsible from "../Collapsible";
import FaqFeedback from "./FaqFeedback";
import type { Faq } from "../../types/types";
import { useViewFaq } from "../../hooks/useFaqs";
import Markdown from "react-markdown";
import remarkBreaks from "remark-breaks";
import type { Components } from "react-markdown";

const markdownComponents: Components = {
    p: (props) => <p className="mb-2 last:mb-0" {...props} />,
    ul: (props) => <ul className="mb-2 list-disc pl-6" {...props} />,
    ol: (props) => <ol className="mb-2 list-decimal pl-6" {...props} />,
    strong: (props) => <strong className="font-semibold" {...props} />,
    h1: (props) => <h3 className="mb-2 text-lg font-semibold" {...props} />,
    h2: (props) => <h3 className="mb-2 text-lg font-semibold" {...props} />,
    h3: (props) => <h3 className="mb-2 text-lg font-semibold" {...props} />,
    a: (props) => (
        <a
            className="underline"
            target="_blank"
            rel="noopener noreferrer"
            {...props}
        />
    ),
};

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
