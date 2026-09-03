import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";
import sv from "./locales/sv.json";
import en from "./locales/en.json";

/**
 * How to use the language library:
 * 
 * 1. Import useTranslation:
 *    import { useTranslation } from 'react-i18next';
 * 
 * 2. Get the translation function (t):
 *    const {t} = useTranslation();
 * 
 * 3. Use t() with the translation key:
 *    t("key.key") ex. t("welcome.title")
 * 
 * 4. To add a new text, add the same key to BOTH language files:
 *    sv.json & en.json
 */
export default i18n
    .use(LanguageDetector)
    .use(initReactI18next)
    .init({
        resources: {
            sv: {translation: sv,},
            en: {translation: en,},
        },
        fallbackLng: "sv",
        supportedLngs: ["sv", "en"],
        interpolation: {
            escapeValue: false,
        },
    });