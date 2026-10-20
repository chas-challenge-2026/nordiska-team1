import InputField from "../../../forms/InputField";
import CategorySelect from "./CategorySelect";
import { QUESTION_MAX, ANSWER_MAX, CATEGORY_MAX, type LANGUAGE_TYPES } from "./InputRules";

type FormData = {
    question: string;
    answer: string;
    category: string;
    categoryNew: string;
    keywords: string;
};

type FormErrors = {
    question?: string;
    answer?: string;
    category?: string;
    categoryNew?: string;
    keywords?: string;
    keywordsFormat?: string;
};

type Props = {
    language: LANGUAGE_TYPES;
    title: string;
    data: FormData;
    errors: FormErrors;
    categorySelectKey: number;

    onQuestionChange: (value: string) => void;
    onAnswerChange: (value: string) => void;
    onCategoryChange: (value: string) => void;
    onCategoryNewChange: (value: string) => void;
    onKeywordsChange: (value: string) => void;
};

export default function FaqForm({
    language,
    title,
    data,
    errors,
    categorySelectKey,
    onQuestionChange,
    onAnswerChange,
    onCategoryChange,
    onCategoryNewChange,
    onKeywordsChange,
}: Props) {
    const isSwedish = language === "sv";

    return (
        <section className="flex flex-col gap-5 flex-1">
            <h2 className="text-xl font-semibold border-b py-2 bg-black text-white text-center">{title}</h2>

            {/* Fråga */}
            <div className="flex flex-col gap-1 bg-gray-200 pt-1 px-2">
                <InputField
                    name={`question${language}`}
                    type="text"
                    label={isSwedish ? "FRÅGA" : "QUESTION"}
                    placeholder={isSwedish? "Skriv frågan på svenska": "Write the question in English"}
                    value={data.question}
                    required
                    error={errors.question}
                    onChange={onQuestionChange}
                />
                <small className="text-right -mt-1">{data.question.length}/{QUESTION_MAX}</small>
            </div>

            {/* Svar */}
            <div className="flex flex-col gap-1 bg-gray-200 pt-1 px-2">
                <InputField
                    name={`answer${language}`}
                    textarea
                    rows={5}
                    label={isSwedish ? "SVAR" : "ANSWER"}
                    placeholder={isSwedish? "Skriv svaret på svenska": "Write the answer in English"}
                    value={data.answer}
                    required
                    error={errors.answer}
                    onChange={onAnswerChange}
                />
                <small className="text-right -mt-3">{data.answer.length}/{ANSWER_MAX}</small>
            </div>

            {/* Kategori */}
            <div className="flex flex-col gap-1 bg-gray-200 pt-1 pb-1.5 px-2">
                <div className="flex items-center justify-between">
                    <label className="text-sm font-bold text-dark-navy">
                        {isSwedish ? "KATEGORI" : "CATEGORY"}{" "}
                        <span className="text-red-600 font-light">*</span>
                    </label>

                    {(errors.category || errors.categoryNew) && (
                        <span className="text-sm text-error"> {errors.category ?? errors.categoryNew}</span>
                    )}
                </div>

                <CategorySelect
                    key={`${language}-${categorySelectKey}`}
                    language={language}
                    value={data.category}
                    onChange={onCategoryChange}
                />

                {data.category === "__new__" && (
                    <InputField
                        name={`category${language}`}
                        type="text"
                        placeholder={isSwedish? "Skriv ny kategori": "Write new category"}
                        value={data.categoryNew}
                        suffix={`${data.categoryNew.length}/${CATEGORY_MAX}`}
                        onChange={onCategoryNewChange}
                    />
                )}
            </div>

            {/* Keywords */}
            <div className="flex flex-col gap-1 bg-gray-200 pt-1 px-2">
                <InputField
                    name={`keywords${language}`}
                    type="text"
                    label="KEYWORDS"
                    placeholder={isSwedish ? "Exempel: ränta, sparande, konto" : "Example: interest, savings, account"}
                    value={data.keywords}
                    required
                    error={errors.keywords}
                    onChange={onKeywordsChange}
                />

                {!errors.keywords && errors.keywordsFormat 
                    ? (<p className="text-sm text-red-600 mr-1">{errors.keywordsFormat}</p>) 
                    : (<p className="text-sm text-gray-500"> Separera keywords med kommatecken.</p>)
                }

            </div>
        </section>
    );
}