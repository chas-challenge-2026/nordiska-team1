import { useState } from "react";
import Modal from "../modals/Modal";
import InputField from "../forms/InputField";
import { CollapsibleFormBtns } from "../forms/CollapsibleFormButtons";
import { useCreateSavingsGoal, useUpdateSavingsGoal, useDeleteSavingsGoal, } from "../../hooks/useAccounts";
import type { SavingGoal } from "../../services/accountsService";
import { useTranslation } from "react-i18next";
import { LoadingState, ErrorState } from "../StatusMessage";

const MAX_TITLE = 30;

type HandleSavingsGoalModalProps = {
    accountNumber: string;
    accountName?: string;
    accountId: number;
    onClose: () => void;
    isModalOpen: boolean;
    onSavingGoalCreated?: () => void;
    existingSavingGoal?: SavingGoal;
    newGoal?: boolean;
};

type SavingsGoalFormData = {
    title: string;
    targetAmount: string;
    targetDate: string;
};

type FormErrors = {
    amountError?: string;
    dateError?: string;
    titleError?: string;
};

export default function HandleSavingsGoalModal({
    accountId,
    accountNumber,
    accountName,
    onClose,
    isModalOpen = false,
    onSavingGoalCreated,
    existingSavingGoal,
    newGoal,
}: HandleSavingsGoalModalProps) {
    const {t} = useTranslation();
    const createSavingGoal = useCreateSavingsGoal();
    const updateSavingGoal = useUpdateSavingsGoal();
    const deleteSavingsGoal = useDeleteSavingsGoal();
    
    const initialFormData: SavingsGoalFormData = {
        targetAmount: !newGoal && existingSavingGoal ? String(existingSavingGoal.targetAmount) : "",
        targetDate: !newGoal && existingSavingGoal?.targetDate ? existingSavingGoal.targetDate.toString().slice(0, 10) : "",
        title: !newGoal && existingSavingGoal ? existingSavingGoal?.title : "",
    };

    const [formData, setFormData] = useState<SavingsGoalFormData>(initialFormData);
    const [hasDate, setHasDate] = useState(!newGoal && !!existingSavingGoal?.targetDate);
    const [errors, setErrors] = useState<FormErrors>({});


    function updateFormField(
        field: keyof SavingsGoalFormData,
        value: string
    ) {
        setFormData((prev) => ({...prev,[field]: value,}));
    }

    function handleClose() {
        resetForm();
        onClose();
    }

    function resetForm() {
        setFormData(initialFormData);
        setHasDate(!newGoal && !!existingSavingGoal?.targetDate);
        setErrors({});
    }

    function validateForm() {
        const newErrors: FormErrors = {};

        const parsedAmount = formData.targetAmount.trim() === "" ? undefined : Number(formData.targetAmount);
        if (parsedAmount === undefined || Number.isNaN(parsedAmount) || parsedAmount <= 0){newErrors.amountError = t("accounts-route.handle-saving-goal.valid-amount");}

        if (hasDate) {
            if (!formData.targetDate) { newErrors.dateError = t("accounts-route.handle-saving-goal.required-date");} 
            else {
                const selectedDate = new Date(formData.targetDate);
                const today = new Date();
                today.setHours(0, 0, 0, 0);
                if (selectedDate <= today) { newErrors.dateError = t("accounts-route.handle-saving-goal.invalid-date");}
            }
        }

        if (formData.title.trim().length === 0) {newErrors.titleError = t("accounts-route.handle-saving-goal.required-title");}
        else if (formData.title.length > MAX_TITLE) {newErrors.titleError = t("accounts-route.handle-saving-goal.title-too-long");} 
        else if (!/^[\p{L}\p{N}\p{M}\s\p{P}\p{S}]+$/u.test(formData.title)) {newErrors.titleError = t("accounts-route.handle-saving-goal.invalid-title");}

        setErrors(newErrors);

        return (
            Object.keys(newErrors).length === 0 &&
            parsedAmount !== undefined
        );
    }

       const handleDelete = () => {
            if (!existingSavingGoal) return;

            const confirmed = window.confirm(`${t("accounts-route.handle-saving-goal.confirm-delete")} ${existingSavingGoal.title}?`);

            if (!confirmed) return;

            deleteSavingsGoal.mutate(existingSavingGoal.id, {
                onSuccess: () => {
                    resetForm();
                    onSavingGoalCreated?.();
                    onClose();
                }
            }) 
        };

    function handleSubmit(e: React.SubmitEvent) {
        e.preventDefault();

        if (!validateForm()) {return;}

        const goalData = {
            title: formData.title.trim(),
            targetAmount: Number(formData.targetAmount),
            targetDate: hasDate ? new Date(formData.targetDate).toISOString() : null,
        };

        // Skapa ett nytt sparmål och stäng det gamla
        if (newGoal && existingSavingGoal) {
            updateSavingGoal.mutate(
                {
                    id: existingSavingGoal.id,
                    status: "completed",
                },
                {
                    onSuccess: () => {
                        createSavingGoal.mutate(
                            {
                                accountId,
                                ...goalData,
                                status: "active",
                            },
                            {
                                onSuccess: () => {
                                    onSavingGoalCreated?.();
                                    onClose();
                                },
                            }
                        );
                    },
                }
            );
            return;
        }

        // Redigera befintligt sparmål
        if (existingSavingGoal && !newGoal) {
            updateSavingGoal.mutate(
                {
                    id: existingSavingGoal.id,
                    ...goalData,
                },
                {
                    onSuccess: () => {
                        onSavingGoalCreated?.();
                        onClose();
                    },
                }
            );
            return;
        }

        // Skapa det första sparmålet
        createSavingGoal.mutate(
            {
                accountId,
                ...goalData,
                status: "active",
            },
            {
                onSuccess: () => {
                    onSavingGoalCreated?.();
                    onClose();
                },
            }
        );
    }

    const isSaving = createSavingGoal.isPending || updateSavingGoal.isPending;
    const hasError = createSavingGoal.isError || updateSavingGoal.isError;

    return (
        <Modal
            isOpen={isModalOpen}
            onClose={handleClose}
            title={existingSavingGoal && !newGoal ? t("accounts-route.handle-saving-goal.edit-title") : t("accounts-route.handle-saving-goal.create-title")}
        >
            {isSaving && (
                <div className="p-10"> <LoadingState message={t("accounts-route.handle-saving-goal.saving")}/> </div>   
            )}
            {hasError && (
                <div className="p-10"> <ErrorState message={t("accounts-route.handle-saving-goal.save-error")}/> </div>
            )}

            {!isSaving && !hasError &&(
                <form onSubmit={handleSubmit} className="flex flex-col gap-4 p-6">
                    <p className="border-y mt-8 text-secondary/70 border-secondary/30">{accountName ? `${accountName} ${accountNumber}` : accountNumber}</p>
                    <h2 className="text-lg font-semibold text-dark-navy"> {existingSavingGoal && !newGoal ? t("accounts-route.handle-saving-goal.edit-title") : t("accounts-route.handle-saving-goal.create-title")} </h2>

                    <InputField
                        name="title"
                        type="text"
                        label={t("accounts-route.handle-saving-goal.title-label")}
                        maxLength={MAX_TITLE}
                        placeholder={t("accounts-route.handle-saving-goal.title-placeholder")}
                        value={formData.title}
                        onChange={(value) => updateFormField("title", value)}
                        error={errors.titleError}
                        required
                    />
                <span className="text-xs text-right -mt-3">{formData.title.length}/{MAX_TITLE}</span>

                    <InputField
                        name="amount"
                        type="number"
                        label={t("accounts-route.handle-saving-goal.amount-label")}
                        placeholder="40000"
                        value={formData.targetAmount}
                        onChange={(value) => updateFormField("targetAmount", value)}
                        error={errors.amountError}
                        suffix="sek"
                        required
                    />

                    <div>
                        <label className="flex cursor-pointer items-center gap-2 text-sm font-medium text-dark-navy">
                            <input
                                type="checkbox"
                                checked={hasDate}
                                onChange={(e) => {
                                    const checked = e.target.checked;
                                    setHasDate(checked);
                                    if (!checked) {
                                        updateFormField("targetDate", "");
                                        setErrors((prev) => ({...prev,dateError: undefined,}));
                                    }
                                }}
                                className="h-4 w-4 cursor-pointer"
                            />
                            {t("accounts-route.handle-saving-goal.date-checkbox")}
                        </label>
                    </div>

                    {hasDate && (
                        <InputField
                            name="targetDate"
                            type="date"
                            placeholder=""
                            label={t("accounts-route.handle-saving-goal.date-checkbox")}
                            value={formData.targetDate}
                            onChange={(value) => updateFormField("targetDate", value)}
                            error={errors.dateError}
                        />
                    )}

                    { existingSavingGoal && !newGoal && (

                        <button
                            type="button"
                            onClick={handleDelete}
                            disabled={deleteSavingsGoal.isPending}
                            className="group my-5 inline-flex min-w-0 flex-1 items-center justify-center rounded-lg bg-white border px-2.5 py-1.5 text-xs font-semibold text-red-600 transition hover:bg-red-600 hover:text-white duration-200 ease-in disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto sm:flex-none sm:text-sm cursor-pointer"
                        >
                            <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-red-600 mask-[url('/icons/close.svg')] duration-200 ease-in mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5 group-hover:bg-white"/>

                            {deleteSavingsGoal.isPending
                                ? t("accounts-route.handle-saving-goal.deleting")
                                : t("accounts-route.handle-saving-goal.delete-goal")}
                        </button>
                    )}

                    <CollapsibleFormBtns onClose={handleClose} submitDisabled={isSaving}/>
                </form>
            )}

        </Modal>
    );
}