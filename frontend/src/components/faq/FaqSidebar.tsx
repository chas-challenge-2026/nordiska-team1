import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import HelpCard from "./HelpCard";

type FaqSidebarProps = {onLanguageChange: () => void;};

export default function FaqSidebar({onLanguageChange}: FaqSidebarProps)  {
    const {t, i18n} = useTranslation();

    const toggleLanguage = () => {
        i18n.changeLanguage(i18n.language === "sv" ? "en" : "sv");
        onLanguageChange?.();
    };

    const helpfulArticles = {
        changeLanguage: {
            title: t("faq.faq-sidebar.question.change-language"),
            answer: t("faq.faq-sidebar.answer.change-language"),
            action: <button
                        type="button"
                        title={i18n.language === "sv" ? "Switch to english" : "Växla till svenska"}
                        onClick={toggleLanguage}
                        className="text-primary-blue text-xs font-semibold cursor-pointer hover:text-primary-hover focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue"
    >
                            {t("faq.faq-sidebar.answer.change-language-action")}
                    </button>,
        },
        updateInfo: {
            title: t("faq.faq-sidebar.question.update-info"),
            answer: t("faq.faq-sidebar.answer.update-info"),
            action: <Link
                        to="/settings"
                        className="text-primary-blue text-xs text-center font-semibold hover:text-primary-hover focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue"
                    >
                        {t("faq.faq-sidebar.answer.update-info-action")}
                    </Link>,
        },
        terms: {
            title: t("faq.faq-sidebar.question.terms"),
            answer: t("faq.faq-sidebar.answer.terms"),
            action: null,
        },
        gdpr: {
            title: t("faq.faq-sidebar.question.gdpr"),
            answer: t("faq.faq-sidebar.answer.gdpr"),
            action: null,
        }
    }

    return (
        <div className="flex w-full min-w-0 flex-col gap-4 lg:flex-1 lg:gap-5 lg:h-full">

            {/* ----- HELPFUL ----- */}
            {/* ------------------- */}
            <article className="bg-white p-4 shadow-md sm:p-5">
                <HelpCard
                    items={helpfulArticles}
                />
            </article>
        </div>
    );
};
