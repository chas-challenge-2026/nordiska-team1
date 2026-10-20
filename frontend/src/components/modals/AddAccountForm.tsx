import { useState } from "react";
import type { SubmitEvent } from "react";
import { useTranslation } from "react-i18next";
import InputField from "../forms/InputField";
import { CollapsibleFormBtns } from "../forms/CollapsibleFormButtons";

export type NewAccountType = "personal" | "bgpg";

export type NewAccountValues = {
    type: NewAccountType;
    clearing: string;
    number: string;
    name: string;
};

type AddAccountFormProps = {
    onCancel: () => void;
    onSave: (values: NewAccountValues) => void;
};

type Field = "clearing" | "number" | "name";

// Förenklade regler för mockade mottagare — ingen kontroll av kontrollsiffra.
// Mellanslag och bindestreck tillåts när man skriver men räknas inte som siffror.
const DIGIT_RULES = {
    clearing: { min: 4, max: 5 },
    personal: { min: 7, max: 10 },
    bgpg: { min: 2, max: 8 },
} as const;

function hasDigits(value: string, { min, max }: { min: number; max: number }) {
    const digits = value.replace(/[\s-]/g, "");
    return /^\d+$/.test(digits) && digits.length >= min && digits.length <= max;
}

/**
 * Innehållet i "lägg till nytt konto"-vyn (renderas inuti `Modal`, i samma
 * modal som kontoväljaren). `onCancel` går tillbaka till kontolistan i
 * samma modal — den stänger inte modalen.
 */
export default function AddAccountForm({ onCancel, onSave }: AddAccountFormProps) {
    const { t } = useTranslation();
    const [type, setType] = useState<NewAccountType | "">("");
    const [clearing, setClearing] = useState("");
    const [number, setNumber] = useState("");
    const [name, setName] = useState("");
    // Fel visas först när man lämnat fältet, inte medan man skriver.
    const [touched, setTouched] = useState<Record<Field, boolean>>({
        clearing: false,
        number: false,
        name: false,
    });

    const isPersonal = type === "personal";

    const errors: Record<Field, string | undefined> = {
        clearing: !isPersonal
            ? undefined
            : !clearing.trim()
                ? t("page-transfer.add-account.error-required")
                : !hasDigits(clearing, DIGIT_RULES.clearing)
                    ? t("page-transfer.add-account.error-clearing")
                    : undefined,
        number: !type
            ? undefined
            : !number.trim()
                ? t("page-transfer.add-account.error-required")
                : !hasDigits(number, DIGIT_RULES[type])
                    ? t(
                        isPersonal
                            ? "page-transfer.add-account.error-number"
                            : "page-transfer.add-account.error-bgpg",
                    )
                    : undefined,
        name: !name.trim()
            ? t("page-transfer.add-account.error-required")
            : undefined,
    };

    const isValid =
        type !== "" && !errors.clearing && !errors.number && !errors.name;

    const touch = (field: Field) => () =>
        setTouched((prev) => ({ ...prev, [field]: true }));

    const shownError = (field: Field) =>
        touched[field] ? errors[field] : undefined;

    const handleTypeChange = (value: NewAccountType) => {
        // Nummerfälten skiljer sig per typ, så börja om på dem.
        setType(value);
        setClearing("");
        setNumber("");
        setTouched((prev) => ({ ...prev, clearing: false, number: false }));
    };

    const handleSubmit = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();
        if (!isValid || !type) return;
        onSave({
            type,
            clearing: isPersonal ? clearing.trim() : "",
            number: number.trim(),
            name: name.trim(),
        });
    };

    return (
        <>
            <div className="border-b border-[#E5EAF0] px-7 py-6 pb-[18px]">
                <h3 className="m-0 text-xl font-semibold text-dark-navy">
                    {t("page-transfer.add-account.heading")}
                </h3>
            </div>

            <form
                onSubmit={handleSubmit}
                noValidate
                className="flex flex-1 flex-col gap-5 overflow-y-auto px-7 py-6"
            >
                <p className="m-0 text-sm text-secondary">
                    {t("page-transfer.add-account.help-text")}{" "}
                    {t("page-transfer.add-account.required-hint")}
                </p>

                <div>
                    <label
                        htmlFor="newAccountType"
                        className="text-sm font-bold text-dark-navy first-letter:uppercase"
                    >
                        {t("page-transfer.add-account.type-label")}{" "}
                        <span className="text-red-600 font-light"> *</span>
                    </label>
                    <select
                        id="newAccountType"
                        name="newAccountType"
                        value={type}
                        required
                        onChange={(e) =>
                            handleTypeChange(e.target.value as NewAccountType)
                        }
                        className="mt-1 w-full rounded-md border border-nordiska-blue bg-white px-3 py-2"
                    >
                        <option value="" disabled>
                            {t("page-transfer.add-account.type-placeholder")}
                        </option>
                        <option value="personal">
                            {t("page-transfer.add-account.type-personal")}
                        </option>
                        <option value="bgpg">
                            {t("page-transfer.add-account.type-bgpg")}
                        </option>
                    </select>
                </div>

                {isPersonal && (
                    <InputField
                        name="newAccountClearing"
                        type="text"
                        label={t("page-transfer.add-account.clearing-label")}
                        placeholder={t(
                            "page-transfer.add-account.clearing-placeholder",
                        )}
                        value={clearing}
                        required
                        onChange={setClearing}
                        onBlur={touch("clearing")}
                        error={shownError("clearing")}
                    />
                )}

                {type && (
                    <InputField
                        name="newAccountNumber"
                        type="text"
                        label={t(
                            isPersonal
                                ? "page-transfer.add-account.number-label"
                                : "page-transfer.add-account.bgpg-label",
                        )}
                        placeholder={t(
                            isPersonal
                                ? "page-transfer.add-account.number-placeholder"
                                : "page-transfer.add-account.bgpg-placeholder",
                        )}
                        value={number}
                        required
                        onChange={setNumber}
                        onBlur={touch("number")}
                        error={shownError("number")}
                    />
                )}

                <InputField
                    name="newAccountName"
                    type="text"
                    label={t("page-transfer.add-account.name-label")}
                    placeholder={t(
                        "page-transfer.add-account.name-placeholder",
                    )}
                    value={name}
                    required
                    onChange={setName}
                    onBlur={touch("name")}
                    error={shownError("name")}
                />

                <CollapsibleFormBtns onClose={onCancel} submitDisabled={!isValid} />
            </form>
        </>
    );
}
