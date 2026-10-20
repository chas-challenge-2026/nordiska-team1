import Fuse from "fuse.js";
import { faqSearchTerms } from "./FaqSearchTerms";

export function getFaqSuggestion( searchTerm: string, language: string): string | null {
    const lang = language.startsWith("en") ? "en" : "sv";
    const term = searchTerm.trim();

    if (!term) return null;

    const fuse = new Fuse(faqSearchTerms[lang], {
        threshold: 0.4,
        ignoreLocation: true,
    });

    const result = fuse.search(term);
    return result[0]?.item ?? null;
}