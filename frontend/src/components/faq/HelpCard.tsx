import { useState, type ReactNode } from "react";
import { useRelevantFaqs } from "../../hooks/useRelevantFaqs";
import { useTranslation } from "react-i18next";

/**
 * HelpCard
 *
 * Reusable card for displaying a list of expandable information items.
 *
 * Props:
 * - items: Object containing items with a title, answer, and optional action.
 * - searchTerms: Optional search terms used to fetch relevant FAQs.
 * - heading: Optional heading for the card.
 *            Default values:
 *            - searchTerms provided: "Relaterat" / "Related"
 *            - items provided: "Mer information" / "More information"
 *
 * ---------------------------------------------------
 * ------------------- USAGE -------------------------
 * ---------------------------------------------------
 *
 * Place HelpCard inside a sized <div> or <article>
 * to control the card's available height and width.
 *
 * Example:
 *
 * <article className="bg-white shadow-md p-4 sm:p-5 max-h-[280px] min-h-[280px]">
 *     <HelpCard
 *         items={helpfulArticles}
 *     />
 * </article>
 *
 * ---------------------------------------------------
 * -------- USING HELPCARD WITH RELEVANT FAQS --------
 * ---------------------------------------------------
 *
 * Pass searchTerms to fetch relevant FAQs from the API:
 *
 * <HelpCard
 *     searchTerms={t("transaction-page.help-card-searchterms")}
 * />
 *
 * ---------------------------------------------------
 * ------- USING HELPCARD WITH A CUSTOM OBJECT -------
 * ---------------------------------------------------
 *
 * Pass a custom object containing the items to display:
 *
 * const items = {
 *     changeLanguage: {
 *         title: "...",
 *         answer: "...",
 *         action: <LanguageButton />,
 *     },
 *     updateInfo: {
 *         title: "...",
 *         answer: "...",
 *         action: <Link to="/settings">...</Link>,
 *     },
 * };
 *
 * <HelpCard
 *     items={items}
 * />
 */

type InfoItem = {
    title: string;
    answer: string;
    action?: ReactNode;
};

type HelpCardProps = {
    heading?: string;
    items?: Record<string, InfoItem>;
    searchTerms?: string;
};

export default function HelpCard({ heading, items, searchTerms,}: HelpCardProps) {
    const {t, i18n} = useTranslation();
    const [selectedItem, setSelectedItem] = useState("");
    const { data: faqs = [], isLoading, isError,} = useRelevantFaqs(i18n.language,searchTerms ?? "");

    const faqItems: Record<string, InfoItem> = Object.fromEntries(
        faqs.map((faq) => [
            faq.id.toString(),
            {
                title: faq.question ?? "",
                answer: faq.answer ?? "",
            },
        ])
    );

    const displayItems: Record<string, InfoItem> = searchTerms ? faqItems : items ?? {};

    return (
        <div className="h-full flex flex-col">
            {searchTerms && isLoading ? (
                <p>{t("generic.loading")}</p>
            ) : searchTerms && isError ? (
                <p>{t("help-card.load-error")}</p>
            ) : (
                <>
                    {/* HEADING */}
                    <div className="border-b border-nordiska-orange pb-1 mb-3">
                        <h2 className="font-semibold text-lg text-nordiska-blue">
                            {heading ? heading : searchTerms ? t("help-card.related") :  t("help-card.information")}
                        </h2>
                    </div>

                    {selectedItem && displayItems[selectedItem] ? (
                        <>
                            {/* TITEL OCH STÄNG */}
                            <div className="flex items-center justify-between pb-2 border-b border-nordiska-blue shrink-0 mb-0">
                                <h3 className="w-full text-left text-sm text-nordiska-blue font-medium">{displayItems[selectedItem].title}</h3>

                                <button
                                    type="button"
                                    onClick={() => setSelectedItem("")}
                                    aria-label={t("generic.close")}
                                    className="h-full min-w-7 max-h-7 aspect-square flex items-center justify-center cursor-pointer rounded-md text-black hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue"
                                >
                                    <span
                                        aria-hidden="true"
                                        className="text-3xl leading-none text-primary-blue hover:text-nordiska-blue"
                                    >
                                        ×
                                    </span>
                                </button>
                            </div>

                            {/* SVAR OCH ACTION */}
                            <div className="flex-1 min-h-0 overflow-y-auto mt-2 pr-2">
                                <div className="flex flex-col gap-4">
                                    <p className="text-sm leading-6">{displayItems[selectedItem].answer}</p>
                                    {displayItems[selectedItem].action}
                                </div>
                            </div>
                        </>
                    ) : (
                        /* LISTA */
                        <ul className="flex flex-col gap-1.5 ">
                            {Object.entries(displayItems).map(
                                ([key, item]) => (
                                    <li key={key} className="flex items-center py-1 hover:bg-gray-100 hover:text-nordiska-blue focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2 transition-colors">
                                        <button
                                            type="button"
                                            onClick={() => setSelectedItem(key)}
                                            className="w-full text-left cursor-pointer text-sm text-primary-blue font-medium leading-4"
                                        >
                                            {item.title}
                                        </button>
                                    </li>
                                )
                            )}
                        </ul>
                    )}
                </>
            )}
        </div>
    );
}