import { useState } from "react";
import CategorySelect from "./CategorySelect";
import { createFaq } from "../../services/faqService";
// import { useNavigate } from "react-router";

export default function CreateFaq() {

    // const navigate = useNavigate();

    const initialFormData = {
        sv: {
            question: "",
            answer: "",
            category: "",
            categoryNew: "",
            keywords: "",
        },
        en: {
            question: "",
            answer: "",
            category: "",
            categoryNew: "",
            keywords: "",
        },
    };

    const [formData, setFormData] = useState(initialFormData);

    const handleClear = () => {
        setFormData(initialFormData);
    };

    const handleCancel = () => {
        setFormData(initialFormData);
    };

    const handleSubmit = async (e: React.SubmitEvent) => {
        e.preventDefault();

        const categorySv =
            formData.sv.category === "__new__"
                ? formData.sv.categoryNew
                : formData.sv.category;


        const categoryEn =
            formData.en.category === "__new__"
                ? formData.en.categoryNew
                : formData.en.category;

        const faqSv = {
            question: formData.sv.question,
            answer: formData.sv.answer,
            category: categorySv,
            keywords: formData.sv.keywords,
            lang: "sv" as const,
        };

        const faqEn = {
            question: formData.en.question,
            answer: formData.en.answer,
            category: categoryEn,
            keywords: formData.en.keywords,
            lang: "en" as const,
        };

        try {
            await createFaq(faqSv);
            await createFaq(faqEn);

            console.log("FAQ skapad på svenska och engelska");
        } catch (error) {
            console.error("Kunde inte skapa FAQ:", error);
        }
    };

    return (
        <form
            onSubmit={handleSubmit}
            className="mt-10 flex flex-col gap-10 max-w-3xl"
        >

            {/* ----------------------------- SVENSKA ----------------------------- */}
            {/* ----------------------------- SVENSKA ----------------------------- */}
            {/* ----------------------------- SVENSKA ----------------------------- */}
            <section className="flex flex-col gap-5">
                <h2 className="text-xl font-semibold border-b py-2 bg-black text-white">
                    SVENSKA
                </h2>

                {/* Titel */}
                <div className="flex flex-col gap-1">
                    <label htmlFor="questionSv">
                        <span className="font-semibold">FRÅGA</span> - svenska
                    </label>

                    <input
                        id="questionSv"
                        name="questionSv"
                        type="text"
                        placeholder="Skriv frågan på svenska"
                        required
                        className="border rounded-md px-3 py-2"
                        value={formData.sv.question}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                sv: {
                                    ...prev.sv,
                                    question: e.target.value,
                                },
                            }))
                        }
                    />
                </div>

                {/* Svar */}
                <div className="flex flex-col gap-1">
                    <label htmlFor="answerSv">
                        <span className="font-semibold">SVAR</span> - svenska
                    </label>

                    <textarea
                        id="answerSv"
                        name="answerSv"
                        placeholder="Skriv svaret på svenska"
                        required
                        rows={6}
                        className="border rounded-md px-3 py-2 resize-y"
                        value={formData.sv.answer}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                sv: {
                                    ...prev.sv,
                                    answer: e.target.value,
                                },
                            }))
                        }
                    />
                </div>

                {/* Kategori */}
                <div className="flex flex-col gap-1">
                    <label>
                        <span className="font-semibold">KATEGORI</span> - svenska
                    </label>

                <CategorySelect
                    language="sv"
                    value={formData.sv.category}
                    onChange={(value) =>
                        setFormData((prev) => ({
                            ...prev,
                            sv: {
                                ...prev.sv,
                                category: value,
                            },
                        }))
                    }
                />

                {formData.sv.category === "__new__" && (
                    <input
                        type="text"
                        placeholder="Skriv ny kategori"
                        className="border rounded-md px-3 py-2"
                        value={formData.sv.categoryNew}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                sv: {
                                    ...prev.sv,
                                    categoryNew: e.target.value,
                                },
                            }))
                        }
                    />
                )}

                </div>

                {/* Keywords */}
                <div className="flex flex-col gap-1">
                    <label htmlFor="keywordsSv">
                        <span className="font-semibold">KEYWORDS</span> - svenska
                    </label>

                    <input
                        id="keywordsSv"
                        name="keywordsSv"
                        type="text"
                        placeholder="Exempel: ränta, sparande, konto"
                        className="border rounded-md px-3 py-2"
                        value={formData.sv.keywords}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                sv: {
                                    ...prev.sv,
                                    keywords: e.target.value,
                                },
                            }))
                        }
                    />

                    <p className="text-sm text-gray-500">
                        Separera keywords med kommatecken.
                    </p>
                </div>
            </section>


            {/* ----------------------------- ENGLISH ----------------------------- */}
            {/* ----------------------------- ENGLISH ----------------------------- */}
            {/* ----------------------------- ENGLISH ----------------------------- */}
            <section className="flex flex-col gap-5">
                <h2 className="text-xl font-semibold border-b py-2 bg-black text-white">

                    ENGELSKA
                </h2>

                {/* Fråga */}
                <div className="flex flex-col gap-1">
                    <label htmlFor="questionEn">
                        <span className="font-semibold">FRÅGA</span> - engelska
                    </label>

                    <input
                        id="questionEn"
                        name="questionEn"
                        type="text"
                        placeholder="Write the question in English"
                        required
                        className="border rounded-md px-3 py-2"
                        value={formData.en.question}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                en: {
                                    ...prev.en,
                                    question: e.target.value,
                                },
                            }))
                        }
                    />
                </div>

                {/* Svar */}
                <div className="flex flex-col gap-1">
                    <label htmlFor="answerEn">
                        <span className="font-semibold">SVAR</span> - engelska
                    </label>

                    <textarea
                        id="answerEn"
                        name="answerEn"
                        placeholder="Write the answer in English"
                        required
                        rows={6}
                        className="border rounded-md px-3 py-2 resize-y"
                        value={formData.en.answer}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                en: {
                                    ...prev.en,
                                    answer: e.target.value,
                                },
                            }))
                        }
                    />
                </div>


                {/* Kategori */}
                <div className="flex flex-col gap-1">
                    <label>
                        <span className="font-semibold">KATEGORI</span> - engelska
                    </label>

                <CategorySelect
                    language="en"
                    value={formData.en.category}
                    onChange={(value) =>
                        setFormData((prev) => ({
                            ...prev,
                            en: {
                                ...prev.en,
                                category: value,
                            },
                        }))
                    }
                />

                {formData.en.category === "__new__" && (
                    <input
                        type="text"
                        placeholder="Skriv ny engelsk kategori"
                        className="border rounded-md px-3 py-2"
                        value={formData.en.categoryNew}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                en: {
                                    ...prev.en,
                                    categoryNew: e.target.value,
                                },
                            }))
                        }
                    />
                )}

                </div>

                {/* Keywords */}
                <div className="flex flex-col gap-1">
                    <label htmlFor="keywordsEn">
                        <span className="font-semibold">KEYWORDS</span> - engelska
                    </label>

                    <input
                        id="keywordsEn"
                        name="keywordsEn"
                        type="text"
                        placeholder="Example: interest, savings, account"
                        className="border rounded-md px-3 py-2"
                        value={formData.en.keywords}
                        onChange={(e) =>
                            setFormData((prev) => ({
                                ...prev,
                                en: {
                                    ...prev.en,
                                    keywords: e.target.value,
                                },
                            }))
                        }
                    />

                    <p className="text-sm text-gray-500">
                        Separate keywords with commas.
                    </p>
                </div>
            </section>


            {/* BUTTONS */}
            <div className="flex justify-end gap-3">
                <button
                    type="button"
                    onClick={handleCancel}
                    className="border-5 border-red-500 rounded-md px-4 py-2 cursor-pointer"
                >
                    Avbryt
                </button>
                <button
                    type="button"
                    onClick={handleClear}
                    className="border-5 border-yellow-500 rounded-md px-4 py-2 cursor-pointer"
                >
                    Rensa
                </button>

                <button
                    type="submit"
                    className="border-5 border-green-500 rounded-md px-4 py-2 cursor-pointer"
                >
                    Skapa FAQ
                </button>
            </div>

        </form>
    );
}