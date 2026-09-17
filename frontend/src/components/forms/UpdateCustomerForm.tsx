import { useState } from "react";
import type { SubmitEvent } from "react";
import InputField from "./InputField";
import { CollapsibleFormBtns } from "./CollapsibleFormButtons";
import { useTranslation } from "react-i18next";
import { updateCustomerData } from "../../services/customerService";
import { useUserStore } from "../../store/userStore";

type CollapsibleFormProps = {
    type: "phone" | "email";
    onClose: () => void;
    onError: (message: string) => void;
    onSuccess: (message: string) => void;
};

export function UpdateCustomerForm({ type, onClose, onError, onSuccess }: CollapsibleFormProps){
    const {t} = useTranslation();

    const [value, setValue] = useState("");
    const [confirmValue, setConfirmValue] = useState("");
    const [error, setError] = useState("");
    const [confirmError, setConfirmError] = useState("");

    const user = useUserStore((state) => state.user );
    const updateUser = useUserStore((state) => state.updateUser);

    let inputType = "";
    let confirmation = false;
    let checkPhoneFormat = false;
    let compareConfirmation = false;

    if (type === "email") {inputType= "email"; confirmation = true; compareConfirmation = true;}
    if (type === "phone") {inputType= "tel"; checkPhoneFormat = true; confirmation = true; compareConfirmation = true;}

    const handleSubmit = async (e:SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();

        setError("");
        onError("");

        if (!user?.id) {
            onError(t("errors.no-user"));
            return;
        }

        const trimmedValue = value.trim().toLowerCase()

        if (checkPhoneFormat) {
            if (!/^07\d{8}$/.test(trimmedValue)) {
                    setError(t("forms.phone-error"));
                    return;
                }
        }

        if (compareConfirmation) {
            if (trimmedValue !== confirmValue.trim().toLowerCase()) {
                    setConfirmError(t("forms.confirm-error"));
                    return;
                }
        }

        const customerData = {[type]: trimmedValue}

        try {
            await updateCustomerData({
                id: user.id,
                ...customerData
            });

            updateUser(customerData);

            setValue("");
            setConfirmValue("");

            onSuccess(t("forms.on-success"));
            onClose();
        } catch (error) {
            onError(t("forms.on-error"));
            console.error(error);
        }
    }
    
    return (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4 font-montserrat">
            <InputField 
                name = {type}
                type = {inputType}
                label = {t(`forms.${type}`)}
                placeholder= {t(`forms.placeholder`)+ t(`forms.${type}`) }
                value={value}
                required
                onChange={setValue}
                error={error}
            />
            { confirmation && (
                <InputField 
                    name = {`${type}Confirmation"`}
                    type = {inputType}
                    label = {t(`forms.confirm`)+ t(`forms.${type}`) }
                    placeholder= {t(`forms.confirm-placeholder`)+ t(`forms.${type}`) }
                    value={confirmValue}
                    required
                    onChange={setConfirmValue}
                    error={confirmError}
                />
            )}
            <CollapsibleFormBtns onClose={onClose} />
        </form>
    )
}