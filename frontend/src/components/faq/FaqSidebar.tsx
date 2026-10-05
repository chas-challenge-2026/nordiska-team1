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
                        className="text-primary-blue text-xs font-semibold cursor-pointer hover:text-nordiska-blue focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue"
    >
                            {i18n.language === "sv" ? "Switch to english" : "Växla till svenska"}
                    </button>,
        },
        updateInfo: {
            title: t("faq.faq-sidebar.question.update-info"),
            answer: t("faq.faq-sidebar.answer.update-info"),
            action: <Link
                        to="/settings"
                        className="text-primary-blue text-xs text-center font-semibold hover:text-nordiska-blue focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue"
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
        <div className="w-full lg:flex-1 flex flex-col gap-5 ml-5">

            {/* ----- HELPFUL ----- */}
            {/* ------------------- */}
            <article className="bg-white shadow-md p-4 sm:p-5 max-h-[280px] min-h-[260px]">
                <HelpCard
                    items={helpfulArticles}
                />
            </article>

            {/* ----- KONTAKT ----- */}
            {/* ------------------- */}
            <article className="bg-white shadow-md p-4 sm:p-5 max-h-[280px] min-h-[260px]">
                <div className="border-b border-nordiska-orange pb-1 mb-3">
                    <h2 className="font-semibold text-lg text-nordiska-blue">{t("faq.faq-sidebar.no-answer-title")}</h2>
                </div>

                <p className="text-sm text-dark-navy font-medium whitespace-pre-line mb-5">{t("faq.faq-sidebar.contact-us")}</p>

                <div className="flex flex-col gap-3 text-dark-navy">
                    <div>
                        <a href="tel:0771123456" className="group flex items-center">
                            <img className="h-5 w-5 mr-2 bg-primary-blue mask-[url('/icons/phone.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-nordiska-blue"/>
                            <span className="font-semibold text-md cursor-pointer text-primary-blue group-hover:text-nordiska-blue">
                                0771-123 456
                            </span>
                        </a>
                        <p className="text-sm ml-7">{t("faq.faq-sidebar.phone-specs")}</p>
                    </div>
                    <div>
                        <a href="mailto:support@nordiska.se" className="group flex items-center">
                            <img className="h-5 w-5 mr-2 bg-primary-blue mask-[url('/icons/envelope.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-nordiska-blue"/>
                            <span className="font-semibold text-md cursor-pointer text-primary-blue group-hover:text-nordiska-blue">
                                support@nordiska.se
                            </span>
                        </a>
                        <p className="text-sm ml-7">{t("faq.faq-sidebar.mail-specs")}</p>
                    </div>
                </div>
            </article>
        </div>
    );
};