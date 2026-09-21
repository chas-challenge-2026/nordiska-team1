import { useTranslation } from "react-i18next";

export default function LanguageButton() {
    const { i18n } = useTranslation();

    const toggleLanguage = () => {
        i18n.changeLanguage(
            i18n.language === "sv" ? "en" : "sv"
        );
    };

    return (
        <button
            type="button"
            title={i18n.language === "sv" ? "Switch to english" : "Växla till svenska"}
            onClick={toggleLanguage}
            className="flex min-h-11 cursor-pointer items-center gap-2 px-4 text-sm font-light uppercase tracking-[0.18em] text-white"
        >
            <img
                className="h-[14px] w-[19px] invert"
                src="icons/lang-icon.svg"
                alt=""
                aria-hidden="true"
            />

            {i18n.language === "sv" ? "English" : "Svenska"}
        </button>
    );
}