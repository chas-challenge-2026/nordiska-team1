import FaqItem from "./FaqItem";
import type { Faq } from "../../types/types";
import { useTranslation } from "react-i18next";
import { LoadingState, ErrorState } from "../StatusMessage";
import NewTicketButton from "../inbox/NewTicket";
import { getFaqSuggestion } from "./FaqCheckSearchTerm";

type FeedbackAction = "increase" | "decrease";

type FaqListProps = {
    faqs: Faq[];
    search: string;
    isLoading: boolean;
    isError: boolean;
    openFaqId: number | null;
    selectedFeedback: Record<number,FeedbackAction>;
    onOpenChange: (id: number, open: boolean) => void;
    onFeedback: ( id: number,action: FeedbackAction) => void;
    onSearchSuggestion: (suggestion: string) => void;
};

const FaqList = ({
    faqs,
    search,
    isLoading,
    isError,
    openFaqId,
    selectedFeedback,
    onOpenChange,
    onFeedback,
    onSearchSuggestion,
}: FaqListProps) => {
    const {t, i18n} = useTranslation();
    const suggestion = getFaqSuggestion(search, i18n.language)
    const showSuggestion = !isLoading && !isError && faqs.length === 0 && !!search.trim() && !!suggestion && suggestion.toLowerCase() !== search.trim().toLowerCase();
    const sortedFaqs = faqs.sort((a,b) => b.helpfulCount - a.helpfulCount);

    if (isLoading) {return (<LoadingState message={t("faq.faq-list.loading")}/>);}
    if (isError) {return ( <ErrorState message={t("faq.faq-list.error")} />);}

    if (faqs.length === 0) {
        return (
            <>
                {showSuggestion 
                    ? (
                        <p> 
                            {t("faq.faq-list.no-results")} {t("faq.faq-list.suggestion")} {" "}
                            <button
                                type="button"
                                onClick={() => onSearchSuggestion(suggestion)}
                                className="font-semibold underline text-primary-blue hover:text-primary-hover cursor-pointer">{suggestion}</button>
                                <span>?</span>
                        </p>
                    )
                    : (
                        <>
                            <p className="font-semibold mb-5">{t("faq-page.no-match-title")}</p>
                            <p className=" text-sm">{t("faq-page.no-match-action")}</p>
                            <NewTicketButton />
                        </>
                    )}

            </>
        );
    }

    return (
        <div className="flex flex-col gap-3">
            {sortedFaqs.map((faq) => (
                <FaqItem
                    key={faq.id}
                    faq={faq}
                    isOpen={openFaqId === faq.id}
                    feedback={selectedFeedback[faq.id]}
                    onOpenChange={(open) => onOpenChange(faq.id, open)}
                    onFeedback={(action) => onFeedback(faq.id, action)}
                />
            ))}
        </div>
    );
};

export default FaqList;