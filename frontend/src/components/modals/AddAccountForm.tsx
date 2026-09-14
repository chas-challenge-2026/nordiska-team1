import { useState } from "react";
import type { SubmitEvent } from "react";
import { useTranslation } from "react-i18next";
import InputField from "../InputField";
import { CollapsibleFormBtns } from "../Buttons";

export type NewAccountValues = {
    type: string;
    clearing: string;
    number: string;
    name: string;
};

type AddAccountFormProps = {
    onCancel: () => void;
    onSave: (values: NewAccountValues) => void;
};

/**
 * Innehållet i "lägg till nytt konto"-vyn (renderas inuti `Modal`, i samma
 * modal som kontoväljaren). `onCancel` går tillbaka till kontolistan i
 * samma modal — den stänger inte modalen.
 */
export default function AddAccountForm({ onCancel, onSave }: AddAccountFormProps) {
    const { t } = useTranslation();
    const [type, setType] = useState("");
    const [clearing, setClearing] = useState("");
    const [number, setNumber] = useState("");
    const [name, setName] = useState("");

    const handleSubmit = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();
        onSave({ type, clearing, number, name });
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
                className="flex flex-1 flex-col gap-5 overflow-y-auto px-7 py-6"
            >
                <p className="m-0 text-sm text-secondary">
                    {t("page-transfer.add-account.help-text")}
                </p>
                <InputField
                    name="newAccountType"
                    type="text"
                    label={t("page-transfer.add-account.type-label")}
                    placeholder={t(
                        "page-transfer.add-account.type-placeholder",
                    )}
                    value={type}
                    onChange={setType}
                />
                <InputField
                    name="newAccountClearing"
                    type="text"
                    label={t("page-transfer.add-account.clearing-label")}
                    placeholder={t(
                        "page-transfer.add-account.clearing-placeholder",
                    )}
                    value={clearing}
                    onChange={setClearing}
                />
                <InputField
                    name="newAccountNumber"
                    type="text"
                    label={t("page-transfer.add-account.number-label")}
                    placeholder={t(
                        "page-transfer.add-account.number-placeholder",
                    )}
                    value={number}
                    onChange={setNumber}
                />
                <InputField
                    name="newAccountName"
                    type="text"
                    label={t("page-transfer.add-account.name-label")}
                    placeholder={t(
                        "page-transfer.add-account.name-placeholder",
                    )}
                    value={name}
                    onChange={setName}
                />

                <CollapsibleFormBtns onClose={onCancel} />
            </form>
        </>
    );
}
