import { useEffect, useState } from "react";
import { getFaqCategories } from "../../../../services/faqService";

type CategorySelectProps = {
    language: string;
    value: string;
    onChange: (value: string) => void;
};

export default function CategorySelect({
    language,
    value,
    onChange,
}: CategorySelectProps) {

    const [categories, setCategories] = useState<string[]>([]);
    const [isLoading, setIsLoading] = useState(true);

    useEffect(() => {
        async function fetchCategories() {
            try {
                setIsLoading(true);
                const data = await getFaqCategories(language);
                setCategories(data);
            } catch (error) {
                console.error("Kunde inte hämta kategorier:", error);
            } finally {
                setIsLoading(false);
            }
        }
        fetchCategories();
    }, [language]);


    return (
        <select
            value={value}
            onChange={(e) => onChange(e.target.value)}
            required
            disabled={isLoading}
            className="min-h-10 bg-white border-2 border-dark-navy rounded-sm px-3 py-2 focus:outline-none focus:border-dark-navy focus:ring-3 focus:ring-nordiska-orange"
        >
            <option value="">{isLoading ? "Laddar kategorier..." : "Välj kategori"}</option>
            <option disabled> ───────────── </option>

            {categories.map((category) => (
                <option key={category} value={category}>
                    {category}
                </option>
            ))}

            <option disabled> ───────────── </option>
            <option value="__new__">Lägg till kategori</option>
        </select>
    );
}