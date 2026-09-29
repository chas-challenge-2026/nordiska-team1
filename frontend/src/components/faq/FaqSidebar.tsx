import { useState } from "react";
import { Link } from "react-router";
import LanguageButton from "../header/LanguageBtn";
import { useTranslation } from "react-i18next";

type RelatedView = "updateInfo" | "changeLanguage" | "";

const FaqSidebar = () => {
    const [showRelated, setShowRelated] =
        useState<RelatedView>("");
    const {t} = useTranslation();

    return (
        <div className="w-full lg:flex-1 flex flex-col gap-5">

            <article className="bg-white shadow-md p-4 sm:p-5 min-h-[280px]">

    <div className="border-b border-nordiska-orange pb-1 mb-3">
        <h2 className="font-semibold text-lg text-nordiska-blue">
            {t("faq.faq-sidebar.related-title")}
        </h2>
    </div>

    {showRelated ? (
        <>
            <div className="flex items-center justify-between gap-4">
                <h3 className="w-full text-left text-sm text-nordiska-blue font-medium ">

                    {showRelated === "updateInfo"
                        ? t("faq.faq-sidebar.question.update-info")
                        : t("faq.faq-sidebar.question.change-language")}
                </h3>

                <button
                    type="button"
                    onClick={() => setShowRelated("")}
                    aria-label={t("faq.faq-sidebar.close-btn-aria-label")}
                    className="shrink-0 w-7 h-7 flex items-center justify-center cursor-pointer rounded-md text-black hover:bg-gray-100 focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue"
                >
                    <span
                        aria-hidden="true"
                        className="text-3xl leading-none text-primary-blue hover:text-nordiska-blue"
                    >
                        ×
                    </span>
                </button>
            </div>

            <div className="mt-5">
                {showRelated === "updateInfo" && (
                    <div className="flex flex-col gap-4">
                        <p className="text-sm leading-6">
                            {t("faq.faq-sidebar.answer.update-info")}
                        </p>

                        <Link
                            to="/settings"
                            className="text-primary-blue text-xs font-semibold hover:text-nordiska-blue focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue"
                        >
                            {t("faq.faq-sidebar.answer.update-info-action")}
                        </Link>
                    </div>
                )}

                {showRelated === "changeLanguage" && (
                    <div className="flex flex-col gap-4">
                        <p className="text-sm leading-6">
                           {t("faq.faq-sidebar.answer.change-language")}
                        </p>

                        <div className="flex items-center invert">
                            <LanguageButton />
                        </div>
                    </div>
                )}
            </div>
        </>
    ) : (
        <ul className="flex flex-col gap-1">
            <li>
                <button
                    type="button"
                    onClick={() => setShowRelated("updateInfo")}
                    className="w-full text-left rounded-md cursor-pointer text-sm text-primary-blue font-medium hover:bg-gray-50 hover:text-nordiska-blue focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2 transition-colors"
                >
                    {t("faq.faq-sidebar.question.update-info")}
                </button>
            </li>

            <li>
                <button
                    type="button"
                    onClick={() => setShowRelated("changeLanguage")}
                    className="w-full text-left  rounded-md cursor-pointer text-sm text-primary-blue font-medium hover:bg-gray-50 hover:text-nordiska-blue focus:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2 transition-colors"
                >
                   
                    {t("faq.faq-sidebar.question.change-language")}
                </button>
            </li>
        </ul>
    )}
</article>

            <article className="bg-dark-navy shadow-md p-4 sm:p-5 flex-1">
                <div className="border-b border-nordiska-orange pb-2 mb-2">
                    <h2 className="font-semibold text-lg text-white">
                        {t("faq.faq-sidebar.no-answer-title")}

                    </h2>
                </div>

                <p className="text-white">
                    {t("faq.faq-sidebar.contact-us")}
                </p>
            </article>

        </div>
    );
};

export default FaqSidebar;