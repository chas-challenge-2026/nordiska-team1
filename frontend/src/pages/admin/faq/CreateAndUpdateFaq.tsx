import { useEffect, useState } from "react";
import { createFaq, editFaq } from "../../../services/faqService";
import { useNavigate, useLocation } from "react-router";
import type { FaqGroup } from "../../../types/types";
import FaqForm from "../../../components/admin/faq/faq-form/FaqForm";
import { QUESTION_MIN, QUESTION_MAX, ANSWER_MIN, ANSWER_MAX, CATEGORY_MAX, KEYWORDS_MAX, KEYWORDS_REGEX, type LANGUAGE_TYPES } from "../../../components/admin/faq/faq-form/InputRules";

const categoryMap = {
    Konto: "Account",
    Account: "Konto",

    Insättning: "Deposit",
    Deposit: "Insättning",

    Ränta: "Interest",
    Interest: "Ränta",

    Rapporter: "Reports",
    Reports: "Rapporter",

    Uttag: "Withdrawal",
    Withdrawal: "Uttag",

    Villkor: "Terms",
    Terms: "Villkor",

    Appen: "App",
    App: "Appen",
} as const;

export default function CreateAndUpdateFaq() {
    const navigate = useNavigate();

// ----- STATES ----- 
    // edit check
    const location = useLocation();
    const editFaqs = location.state?.faqGroup as FaqGroup | undefined;

    // kategori
    const [categorySelectKey, setCategorySelectKey] = useState(0);

    // initial input
    const svFaq = editFaqs?.faqs.find((faq) => faq.lang === "sv");
    const enFaq = editFaqs?.faqs.find((faq) => faq.lang === "en");
    const initialFormData = {
        sv: {
            question: svFaq?.question ?? "",
            answer: svFaq?.answer ?? "",
            category: svFaq?.category ?? "",
            categoryNew: "",
            keywords: svFaq?.keywords?.join(", ") ?? "",
        },
        en: {
            question: enFaq?.question ?? "",
            answer: enFaq?.answer ?? "",
            category: enFaq?.category ?? "",
            categoryNew: "",
            keywords: enFaq?.keywords?.join(", ") ?? "",
        },
    };
    const [formData, setFormData] = useState(initialFormData);

    // errors
    type FormErrors = {
        question?: string;
        answer?: string;
        category?: string;
        categoryNew?: string;
        keywords?: string;
        keywordsFormat?: string;
    };
    const [errors, setErrors] = useState<{ sv: FormErrors; en: FormErrors; }>({ sv: {}, en: {},});

    // user feedback
    const [updateFeedback, setUpdateFeedback] = useState(false);
    const [updateFeedbackSuccess, setUpdateFeedbackSuccess] = useState("");
    const [updateFeedbackPartial, setUpdateFeedbackPartial] = useState("");
    const [updateFeedbackFail, setUpdateFeedbackFail] = useState("");

// ----- VISA USER FEEDBACK -----
    useEffect(() => {
        const timer = setTimeout(() => {
            setUpdateFeedback(false)
        }, 4000);
        return () => clearTimeout(timer);
    }, [updateFeedback])

// ----- VALIDERING ----- 
    // Fråga och svar
    function validateQuestionAndAnswer( type: "question" | "answer", lang: LANGUAGE_TYPES, value: string) {

        const MIN_LIMIT = type === "question" ? QUESTION_MIN : ANSWER_MIN;
        const MAX_LIMIT = type === "question" ? QUESTION_MAX : ANSWER_MAX;

        setFormData((prev) => ({...prev, [lang]: { ...prev[lang], [type]: value}}));
        setErrors((prev) => {
            const newErrors = { ...prev[lang]};
            const fieldName = type === "question" ? "Frågan" : "Svaret";
            if (!value.trim()) {newErrors[type] = type === "question" ? "Ange en fråga" : "Ange ett svar"}
            else if (value.length < MIN_LIMIT) { newErrors[type] = `${fieldName} måste vara minst ${MIN_LIMIT} tecken`;}
            else if (value.length > MAX_LIMIT) { newErrors[type] = `${fieldName} får vara max ${MAX_LIMIT} tecken`}
            else { delete newErrors[type]}
            return {...prev, [lang]: newErrors,}
        });
    }
    // Ny kategori
    function validateCategory( lang: LANGUAGE_TYPES, value: string) {
        setFormData((prev) => ({...prev, [lang]: { ...prev[lang], categoryNew: value}}));
        setErrors((prev) => {
            const newErrors = {...prev[lang]};
            if (!value.trim()) {newErrors.categoryNew = "Ange den nya kategorin";} 
            else if (value.length > CATEGORY_MAX) {newErrors.categoryNew = `Kategorin får vara max ${CATEGORY_MAX} tecken`;}
            else { delete newErrors.categoryNew}
            return {...prev, [lang]: newErrors,}
        })
    }
    // Keywords
    function validateKeywords(lang: LANGUAGE_TYPES, value: string ) {
        setFormData((prev) => ({...prev, [lang]: { ...prev[lang], keywords: value}}));
        setErrors((prev) => {
            const newErrors = {...prev[lang]};
            if (!value.trim()) {newErrors.keywords = "Ange minst ett keyword";}
            else if (value.length > KEYWORDS_MAX) {newErrors.keywords = `Keywords får vara max ${KEYWORDS_MAX} tecken`;}
            else if (!KEYWORDS_REGEX.test(value)) {newErrors.keywordsFormat ="Fel format: Ange keywords separerade med kommatecken (ränta, uttag, konto)";}
            else { delete newErrors.keywords; delete newErrors.keywordsFormat}
            return {...prev, [lang]: newErrors,}
        })
    }
    // Synka och validera befintlig kategori
    function syncAndValidateCategories( lang: LANGUAGE_TYPES, value: string) {
        setErrors((prev) => {
            const newErrors = {...prev,
                sv: { ...prev.sv },
                en: { ...prev.en },
            };
            delete newErrors.sv.categoryNew;
            delete newErrors.en.categoryNew;

            if (!value.trim()) {newErrors[lang].category = "Välj en kategori";} 
            else {delete newErrors[lang].category;}
            return newErrors;
        });

        if (value === "__new__") {
            setFormData((prev) => ({...prev,
                sv: { ...prev.sv, category: "__new__" },
                en: { ...prev.en, category: "__new__" },
            }));
            return;
        }

        const translatedCategory = categoryMap[value as keyof typeof categoryMap];

        if (translatedCategory) {
            setFormData((prev) => ({...prev,
                sv: {...prev.sv,
                    category: lang === "sv" ? value : translatedCategory,},
                en: {...prev.en,
                    category: lang === "en" ? value : translatedCategory,},
            }));
            return;
        }

        setFormData((prev) => ({...prev,
            [lang]: {...prev[lang],
                category: value,
            },
        }));
    }
    // Tomma fält
    function validateForm() {
        const newErrors: { sv: FormErrors; en: FormErrors; } = { sv: {}, en: {},};

        (["sv", "en"] as LANGUAGE_TYPES[]).forEach((lang) => {
            const data = formData[lang];

            if (!data.question.trim()) {newErrors[lang].question = "Ange en fråga";} 
            if (!data.answer.trim()) {newErrors[lang].answer = "Ange ett svar";} 
            if (!data.category.trim()) {newErrors[lang].category = "Välj en kategori";} 
            else if (data.category === "__new__") {
                if (!data.categoryNew.trim()) {newErrors[lang].categoryNew ="Ange den nya kategorin";} 
            }
            if (!data.keywords.trim()) {newErrors[lang].keywords = "Ange minst ett keyword";} 
        });

        setErrors(newErrors);

        return (
            Object.keys(newErrors.sv).length === 0 &&
            Object.keys(newErrors.en).length === 0
        );
    }
    const isFormValid = Boolean(formData.sv.question.trim() && formData.sv.answer.trim() && formData.sv.category.trim() && formData.sv.keywords.trim() && formData.en.question.trim() && formData.en.answer.trim() && formData.en.category.trim() && formData.en.keywords.trim() && Object.keys(errors.sv).length === 0 && Object.keys(errors.en).length === 0);

// ----- HANDLERS ----- 
    const handleClear = () => {
        setFormData(initialFormData);
        setErrors({sv: {},en: {},});
    };

    const handleCancel = () => {
        setFormData(initialFormData);
        setErrors({sv: {},en: {},});

        if (editFaqs) {navigate("/admin/faq/edit")} 
        else {navigate("/admin")}
    };

    // hantera redigera OCH skapa faq 
    const handleSubmit = async (e: React.SubmitEvent) => {
        e.preventDefault();

        const isValid = validateForm();
        if (!isValid) {return;}

        const categorySv = formData.sv.category === "__new__" ? formData.sv.categoryNew : formData.sv.category;
        const categoryEn = formData.en.category === "__new__" ? formData.en.categoryNew : formData.en.category;

        try {

            // UPPDATERA REDIGERAD FAQ
            if ( editFaqs) {
                const svFaq = editFaqs.faqs.find((faq) => faq.lang === "sv");
                const enFaq = editFaqs.faqs.find((faq) => faq.lang === "en");
                if (!svFaq || !enFaq) {throw new Error("Ett eller båda språken saknas.");}

                try {
                    await editFaq(svFaq.id, svFaq.lang, {
                        question: formData.sv.question,
                        answer: formData.sv.answer,
                        category: categorySv,
                        keywords: formData.sv.keywords,
                    });
                } catch (error) {
                    console.log("Kunde inte spara redigerad FAQ på svenska: ", error);
                    setUpdateFeedback(true);
                    setUpdateFeedbackFail("Kunde inte spara redigerad FAQ på svenska");
                    return;
                }

                try {
                    await editFaq(enFaq.id, enFaq.lang, {
                        question: formData.en.question,
                        answer: formData.en.answer,
                        category: categoryEn,
                        keywords: formData.en.keywords,
                    });
                } catch (error) {
                    console.log("Kunde inte spara redigerad FAQ på engelska: ", error);
                    setUpdateFeedback(true);
                    setUpdateFeedbackPartial("Redigerad FAQ på svenska sparad. Kunde inte spara redigerad FAQ på engelska");
                    return;
                }

                navigate("/admin/faq/edit", {
                    state: {
                        feedback: "Redigerad FAQ uppdaterad."
                    }
                });
            
            } 
            // SKAPA FAQ
            else {
                const relationId = crypto.randomUUID();

                try {
                    await createFaq({
                        question: formData.sv.question,
                        answer: formData.sv.answer,
                        category: categorySv,
                        keywords: formData.sv.keywords,
                        lang: "sv" as const,
                        relationId
                    })
                } catch (error) {
                    console.log("Kunde inte spara ny FAQ på svenska: ", error);
                    setUpdateFeedback(true);
                    setUpdateFeedbackFail("Kunde inte spara ny FAQ på svenska");
                    return;
                }

                try {
                    await createFaq({
                        question: formData.en.question,
                        answer: formData.en.answer,
                        category: categoryEn,
                        keywords: formData.en.keywords,
                        lang: "en" as const,
                        relationId
                    })
                } catch (error) {
                    console.log("Kunde inte spara ny FAQ på engelska: ", error);
                    setUpdateFeedback(true);
                    setUpdateFeedbackPartial("Ny FAQ på svenska sparad. Kunde inte spara ny FAQ på engelska");
                    return;
                }
                setUpdateFeedback(true);
                setUpdateFeedbackSuccess("Ny FAQ skapad.");
                setCategorySelectKey(prev => prev + 1);
                setFormData(initialFormData);
            }
        }  catch (error) {
            console.error("Kunde inte spara FAQ:", error);
            setUpdateFeedback(true);
            setUpdateFeedbackFail("Något gick fel")
        }
    };

{/* ----------------------------- FORMULÄR ----------------------------- */}
{/* -------------------------------------------------------------------- */}
    return (
    <>
        <h1 className="text-3xl font-semibold py-2.5 bg-black text-white text-center">{editFaqs ? "Redigera" : "Skapa ny"}</h1>
        <form onSubmit={handleSubmit} className="mt-10 flex-col gap-10 max-w">
            <div className="flex gap-5">
                {/* ----------------------------- SVENSKA ----------------------------- */}
                {/* ------------------------------------------------------------------- */}
                <FaqForm
                    language="sv"
                    title="SVENSKA"
                    data={formData.sv}
                    errors={errors.sv}
                    categorySelectKey={categorySelectKey}
                    onQuestionChange={(value) => validateQuestionAndAnswer("question", "sv", value)}
                    onAnswerChange={(value) => validateQuestionAndAnswer("answer", "sv", value)}
                    onCategoryChange={(value) => syncAndValidateCategories("sv", value)}
                    onCategoryNewChange={(value) => validateCategory("sv", value) }
                    onKeywordsChange={(value) => validateKeywords("sv", value) }
                />

                {/* ----------------------------- ENGLISH ----------------------------- */}
                {/* ------------------------------------------------------------------- */}
                <FaqForm
                    language="en"
                    title="ENGELSKA"
                    data={formData.en}
                    errors={errors.en}
                    categorySelectKey={categorySelectKey}
                    onQuestionChange={(value) => validateQuestionAndAnswer("question", "en", value)}
                    onAnswerChange={(value) => validateQuestionAndAnswer("answer", "en", value)}
                    onCategoryChange={(value) => syncAndValidateCategories("en", value)}
                    onCategoryNewChange={(value) => validateCategory("en", value) }
                    onKeywordsChange={(value) => validateKeywords("en", value) }
                />
            </div>

            {/* BUTTONS */}
            {!updateFeedback &&
                <div className="flex justify-center gap-10 mt-10">
                    <button
                        type="button"
                        onClick={handleCancel}
                        className="border-5 bg-red-500 border-black rounded-md px-4 py-2 cursor-pointer font-semibold hover:bg-white hover:text-black hover:border-red-500"
                    >
                        Avbryt
                    </button>
                    <button
                        type="button"
                        onClick={handleClear}
                        disabled={editFaqs ? false : !isFormValid}
                        className="border-5 bg-yellow-500 border-black rounded-md px-4 py-2 cursor-pointer font-semibold hover:bg-white hover:text-black hover:border-yellow-500 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-yellow-500 disabled:hover:border-black "
                    >
                        {editFaqs ? "Återställ" : "Rensa"}
                    </button>

                    <button
                        type="submit"
                        disabled={!isFormValid}
                        className="border-5 bg-green-500 border-black rounded-md px-4 py-2 cursor-pointer font-semibold hover:bg-white hover:text-black hover:border-green-500 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-green-500 disabled:hover:border-black"
                    >
                        {editFaqs ? "Uppdatera" : "Skapa ny"}
                    </button>
                </div>
            }

            {updateFeedback && 
                <div className={`mt-10 border-5 ${updateFeedbackFail ? "bg-red-500" : updateFeedbackPartial ? "bg-yellow-500": "bg-green-500"} border-black rounded-md px-4 py-2 cursor-pointer font-semibold `}>
                    <p className="text-center">
                        {updateFeedbackFail ? updateFeedbackFail : updateFeedbackPartial ? updateFeedbackPartial : updateFeedbackSuccess}
                    </p>
                </div>
            }
        </form>
    </>
    );
}