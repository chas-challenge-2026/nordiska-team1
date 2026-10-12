import { useState } from "react";
import Modal from "../modals/Modal";
import InputField from "../forms/InputField";
import { CollapsibleFormBtns } from "../forms/CollapsibleFormButtons";
import { useCreateSavingsGoal, useUpdateSavingsGoal, useDeleteSavingsGoal, } from "../../hooks/useAccounts";
import type { SavingGoal } from "../../services/accountsService";

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
        if (parsedAmount === undefined || Number.isNaN(parsedAmount) || parsedAmount <= 0){newErrors.amountError = "Ange ett giltigt belopp.";}

        if (hasDate) {
            if (!formData.targetDate) { newErrors.dateError = "Ange ett slutdatum";} 
            else {
                const selectedDate = new Date(formData.targetDate);
                const today = new Date();
                today.setHours(0, 0, 0, 0);
                if (selectedDate <= today) { newErrors.dateError = "Slutdatumet måste vara längre fram i tiden";}
            }
        }

        if (formData.title.trim().length === 0) {newErrors.titleError = "Ange en titel";} 
        else if (!/^[a-zA-ZåäöÅÄÖ0-9 &'’-]+$/.test(formData.title.trim())) {newErrors.titleError ="Endast tecken - ' & är tillåtna i titeln";}

        setErrors(newErrors);

        return (
            Object.keys(newErrors).length === 0 &&
            parsedAmount !== undefined
        );
    }

       const handleDelete = () => {
            if (!existingSavingGoal) return;

            const confirmed = window.confirm(
                `Är du säker på att du vill ta bort sparmål ${existingSavingGoal.title}?`
            );

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
            title={existingSavingGoal && !newGoal ? "Redigera sparmål" : "Sätt ett sparmål"}
        >
            {isSaving && (<p className="text-sm text-red-700"> Sparar ditt sparmål... </p>)}
            {hasError && (<p className="text-sm text-red-700"> Kunde inte spara sparmålet. Försök igen. </p>)}

            <form onSubmit={handleSubmit} className="flex flex-col gap-4 p-6">

                <p className="border-y mt-8 text-secondary/70 border-secondary/30">{accountName ? `${accountName} ${accountNumber}` : accountNumber}</p>
                <h2 className="text-lg font-semibold text-dark-navy"> {existingSavingGoal && !newGoal ? "Redigera sparmål" : "Sätt ett sparmål"} </h2>

                <InputField
                    name="title"
                    type="text"
                    label="Namnge ditt sparmål?"
                    placeholder="ex. Köp ny TV"
                    value={formData.title}
                    onChange={(value) => updateFormField("title", value)}
                    error={errors.titleError}
                    required
                />

                <InputField
                    name="amount"
                    type="number"
                    label="Ange ditt sparmål"
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
                        Jag vill ange ett slutdatum för mitt sparande
                    </label>
                </div>

                {hasDate && (
                    <InputField
                        name="targetDate"
                        type="date"
                        placeholder=""
                        label="Slutdatum"
                        value={formData.targetDate}
                        onChange={(value) => updateFormField("targetDate", value)}
                        error={errors.dateError}
                    />
                )}

                { existingSavingGoal && !newGoal &&
                (

                                    <button
                        type="button"
                        onClick={handleDelete}
                        disabled={deleteSavingsGoal.isPending}
                        className="group my-5 inline-flex min-w-0 flex-1 items-center justify-center rounded-lg bg-white border px-2.5 py-1.5 text-xs font-semibold text-red-600 transition hover:bg-red-600 hover:text-white duration-200 ease-in disabled:cursor-not-allowed disabled:opacity-60 sm:w-auto sm:flex-none sm:text-sm cursor-pointer"
                    >
                        <span
                            aria-hidden="true"
                            className="mr-1.5 block h-4 w-4 bg-red-600 mask-[url('/icons/close.svg')] duration-200 ease-in mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5 group-hover:bg-white"
                        />
                        {deleteSavingsGoal.isPending
                            ? "Tar bort..."
                            : "Ta bort sparmål"}
                    </button>


                )



                }



                <CollapsibleFormBtns onClose={handleClose} submitDisabled={isSaving}/>
            </form>
        </Modal>
    );
}