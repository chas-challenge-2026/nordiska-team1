import { useState } from "react";
import type { SubmitEvent } from "react";
import { useTranslation } from "react-i18next";
import InputField from "./InputField";
import { CollapsibleFormBtns } from "./Buttons";

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
 * Ersätter överföringsformuläret i vänsterkolumnen när användaren lägger
 * till ett nytt mottagarkonto från kontoväljar-modalen.
 */
export default function AddAccountForm({
    onCancel,
    onSave,
}: AddAccountFormProps) {
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
        <div className="animate-rise">
            <div className="flex items-end justify-between border-b-[3px] border-nordiska-orange pb-2.5">
                <h2 className="m-0 text-[26px] font-semibold text-dark-navy">
                    {t("page-transfer.add-account.heading")}
                </h2>
            </div>
            <p className="mt-3.5 mb-6.5 text-sm text-secondary">
                {t("page-transfer.add-account.help-text")}
            </p>

            <form
                onSubmit={handleSubmit}
                className="flex max-w-[460px] flex-col gap-5"
            >
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
        </div>
    );
}
