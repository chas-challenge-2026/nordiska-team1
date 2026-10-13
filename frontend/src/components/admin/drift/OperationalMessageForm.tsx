
import { useState, type SubmitEvent } from "react";
import { createOperationalMessage, updateOperationalMessage, type CreateOperationalMessageRequest,} from "../../../services/adminService";
import { useServiceAlertStore } from "../../../store/serviceAlertStore";
import InputField from "../../forms/InputField";

export type OperationalMessageData = {
    id: number;
    title: {
        sv: string | null;
        en: string | null;
    };
    message: {
        sv: string | null;
        en: string | null;
    };
    severity: string | null;
    isActive: boolean;
    priority: number;
    startDate: string | null;
    endDate: string | null;
};

type FormValues = {
    titleSv: string;
    titleEn: string;
    messageSv: string;
    messageEn: string;
    severity: string;
    isActive: boolean;
    priority: number;
    startDate: string;
    endDate: string;
};

type Props = {
    item?: OperationalMessageData;
    onCancel?: () => void;
    onSaved: () => void | Promise<void>;
};

function toLocalDateTime(value: string | null) {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    return new Date( date.getTime() - date.getTimezoneOffset() * 60_000,).toISOString().slice(0, 16);
}

function getInitialValues(item?: OperationalMessageData,): FormValues {
    return {
        titleSv: item?.title?.sv ?? "",
        titleEn: item?.title?.en ?? "",
        messageSv: item?.message?.sv ?? "",
        messageEn: item?.message?.en ?? "",
        severity: item?.severity ?? "info",
        isActive: item?.isActive ?? true,
        priority: item?.priority ?? 1,
        startDate: toLocalDateTime(item?.startDate ?? null),
        endDate: toLocalDateTime(item?.endDate ?? null),
    };
}

export default function OperationalMessageForm({item, onCancel, onSaved,}: Props) {
    const [form, setForm] = useState<FormValues>(() => getInitialValues(item),);
    const [saving, setSaving] = useState(false);
    const [error, setError] = useState("");

    function updateField<K extends keyof FormValues>(
        field: K,
        value: FormValues[K],
    ) {
        setForm((previous) => ({...previous, [field]: value,}));
    }

    async function handleSubmit(event: SubmitEvent<HTMLFormElement>) {
        event.preventDefault();

        if (!Number.isInteger(form.priority) || form.priority < 0) {
            setError("Prioriteten måste vara ett heltal som är minst 0.");
            return;
        }

        if (form.startDate && form.endDate && new Date(form.endDate) < new Date(form.startDate)) {
            setError("Slutdatumet kan inte vara före startdatumet.");
            return;
        }

        setSaving(true);
        setError("");

        const payload: CreateOperationalMessageRequest = {
            titleSv: form.titleSv.trim() || null,
            titleEn: form.titleEn.trim() || null,
            messageSv: form.messageSv.trim() || null,
            messageEn: form.messageEn.trim() || null,
            severity: form.severity || null,
            isActive: form.isActive,
            priority: form.priority,
            startDate: form.startDate
                ? new Date(form.startDate).toISOString()
                : null,
            endDate: form.endDate
                ? new Date(form.endDate).toISOString()
                : null,
        };

        try {
            if (item) {
                await updateOperationalMessage(item.id, payload);
                const store = useServiceAlertStore.getState();
                if (store.alert?.id === String(item.id)) {store.closeAlert();}
            } else {
                await createOperationalMessage(payload);
            }
            await onSaved();
        } catch (err) {
            console.error(err);
            setError(item ? "Kunde inte uppdatera driftmeddelandet.": "Kunde inte skapa driftmeddelandet.",);
        } finally {
            setSaving(false);
        }
    }

    return (
        <form onSubmit={handleSubmit} className="space-y-4">

            <div className="grid gap-4 sm:grid-cols-2">

                <div>
                    <h2 className="text-xl font-semibold border-b py-2 mb-2 bg-black text-white text-center">Svenska</h2>
                    <div className="flex flex-col gap-1 bg-gray-200 p-2">
                        <InputField 
                            name="sv-title"
                            type="text"
                            label="TITEL"
                            placeholder="Titel på svenska"
                            value={form.titleSv}
                            required
                            maxLength={50}
                            onChange={(value) => updateField("titleSv", value)}
                        />
                    </div>
                </div>

                <div>
                    <h2 className="text-xl font-semibold border-b py-2 mb-2 bg-black text-white text-center">Engelska</h2>
                    <div className="flex flex-col gap-1 bg-gray-200 p-2">
                        <InputField 
                            name="en-title"
                            type="text"
                            label="TITLE"
                            placeholder="Titel på engelska"
                            value={form.titleEn}
                            required
                            maxLength={50}
                            onChange={(value) => updateField("titleEn", value)}
                        />
                    </div>
                </div>

                <div className="flex flex-col gap-1 bg-gray-200 p-2">
                    <InputField 
                            name="sv-message"
                            textarea
                            rows={3}
                            label="MEDDELANDE"
                            placeholder="Meddelande på svenska"
                            value={form.messageSv}
                            required
                            maxLength={250}
                            onChange={(value) => updateField("messageSv", value)}
                        />
                </div>

                <div className="flex flex-col gap-1 bg-gray-200 p-2">
                    <InputField 
                            name="en-message"
                            textarea
                            rows={3}
                            label="MESSAGE"
                            placeholder="Message in english"
                            value={form.messageEn}
                            required
                            maxLength={250}
                            onChange={(value) => updateField("messageEn", value)}
                        />
                </div>
            </div>
                
            <h2 className="text-xl font-semibold border-b py-2 bg-black text-white text-center">Inställningar</h2>
            
            <div className="space-y-4 bg-gray-200 p-4">
                <div className="grid gap-4 sm:grid-cols-2">

                    <label className="flex flex-col gap-1">
                        <span className="text-sm font-bold text-dark-navy">
                            ALLVARLIGHETSGRAD
                        </span>
                        <select
                            className="w-full rounded-md border border-nordiska-blue bg-white px-3 py-2"
                            value={form.severity}
                            onChange={(e) => updateField("severity", e.target.value)}
                        >
                            <option value="info">Information</option>
                            <option value="warning">Varning</option>
                            <option value="error">Driftstörning</option>
                        </select>
                    </label>

                    <InputField
                        name="priority"
                        type="number"
                        label="PRIORITET"
                        placeholder="Ange prioritet"
                        value={String(form.priority)}
                        required
                        onChange={(value) =>
                            updateField("priority", value === "" ? 0 : Number(value))
                        }
                    />

                    <label className="flex flex-col gap-1">
                        <span className="text-sm font-bold text-dark-navy">
                            STARTDATUM
                        </span>
                        <input
                            type="datetime-local"
                            className="w-full rounded-md border border-nordiska-blue bg-white px-3 py-2"
                            value={form.startDate}
                            onChange={(e) => updateField("startDate", e.target.value)}
                        />
                    </label>

                    <label className="flex flex-col gap-1">
                        <span className="text-sm font-bold text-dark-navy">
                            SLUTDATUM
                        </span>
                        <input
                            type="datetime-local"
                            className="w-full rounded-md border border-nordiska-blue bg-white px-3 py-2"
                            value={form.endDate}
                            onChange={(e) => updateField("endDate", e.target.value)}
                        />
                    </label>
                </div>

                <label className="flex cursor-pointer items-center mt-6 gap-2 text-base ml-1 font-bold text-dark-navy">
                    <input
                        type="checkbox"
                        checked={form.isActive}
                        onChange={(e) => updateField("isActive", e.target.checked)}
                        className="size-4 accent-nordiska-blue"
                    />
                    Aktivt driftmeddelande
                </label>
            </div>

            {error && (<p role="alert" className="text-sm text-red-600">{error}</p>)}

            <div className="flex flex-wrap justify-end gap-2">
                {onCancel && (
                    <button
                        type="button"
                        onClick={onCancel}
                        disabled={saving}
                        className="border-5 bg-red-500 border-black rounded-md px-4 py-2 cursor-pointer font-semibold hover:bg-white hover:text-black hover:border-red-500"
                    >
                        Avbryt
                    </button>
                )}

                <button
                    type="submit"
                    disabled={saving}
                    className="border-5 bg-green-500 border-black rounded-md px-4 py-2 cursor-pointer font-semibold hover:bg-white hover:text-black hover:border-green-500 disabled:cursor-not-allowed disabled:opacity-50 disabled:hover:bg-green-500 disabled:hover:border-black"
                >
                    {saving
                        ? "Sparar..."
                        : item
                          ? "Spara ändringar"
                          : "Skapa driftmeddelande"}
                </button>
            </div>
        </form>
    );
}