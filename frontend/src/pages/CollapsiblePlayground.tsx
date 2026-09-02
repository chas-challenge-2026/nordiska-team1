import { useState } from "react";
import type { SubmitEvent } from "react";
import Collapsible from "../components/Collapsible";

/**
 * Tillfällig testsida för att kolla Collapsible visuellt innan
 * backend finns. Tas bort/ersätts när komponenten används på riktigt.
 */
type OpenField = "email" | "phone" | null;

export default function CollapsiblePlayground() {
    // Simulerar det som senare kommer från backend: sparat värde eller inget.
    const [savedEmail, setSavedEmail] = useState<string | undefined>(undefined);
    const [savedPhone, setSavedPhone] = useState<string | undefined>(
        "070***4501"
    );
    // Bara ett fält kan vara öppet åt gången (accordion).
    const [openField, setOpenField] = useState<OpenField>(null);

    return (
        <div className="mx-auto mt-20 max-w-xl px-4">
            <Collapsible
                title="Email"
                preview={savedEmail}
                isOpen={openField === "email"}
                onOpenChange={(open) => setOpenField(open ? "email" : null)}
            >
                {(close) => (
                    <EmailForm
                        onCancel={close}
                        onSave={(value) => {
                            setSavedEmail(value);
                            close();
                        }}
                    />
                )}
            </Collapsible>

            <Collapsible
                title="Telefonnummer"
                preview={savedPhone}
                isOpen={openField === "phone"}
                onOpenChange={(open) => setOpenField(open ? "phone" : null)}
            >
                {(close) => (
                    <PhoneForm
                        onCancel={close}
                        onSave={(value) => {
                            setSavedPhone(value);
                            close();
                        }}
                    />
                )}
            </Collapsible>
        </div>
    );
}

type EmailFormProps = {
    onCancel: () => void;
    onSave: (value: string) => void;
};

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function EmailForm({ onCancel, onSave }: EmailFormProps) {
    const [email, setEmail] = useState("");
    const [repeatEmail, setRepeatEmail] = useState("");
    const [error, setError] = useState("");

    const handleSave = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();
        if (!EMAIL_REGEX.test(email)) {
            setError("Ange en giltig e-postadress");
            return;
        }
        if (email !== repeatEmail) {
            setError("E-postadresserna matchar inte");
            return;
        }
        setError("");
        onSave(email);
    };

    return (
        <form onSubmit={handleSave} className="flex flex-col gap-4 font-montserrat">
            <div>
                <label className="text-sm font-bold text-dark-navy">Email</label>
                <input
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    placeholder="Ange email"
                    className="mt-1 w-full rounded-md border border-nordiska-blue px-3 py-2 placeholder:text-gray-400"
                />
            </div>

            <div>
                <div className="flex items-center justify-between">
                    <label className="text-sm font-bold text-dark-navy">
                        Upprepa email
                    </label>
                    {error && <span className="text-sm text-red-600">{error}</span>}
                </div>
                <input
                    type="email"
                    value={repeatEmail}
                    onChange={(e) => setRepeatEmail(e.target.value)}
                    placeholder="Upprepa email"
                    className="mt-1 w-full rounded-md border border-nordiska-blue px-3 py-2 placeholder:text-gray-400"
                />
            </div>

            <div className="flex justify-end gap-6 pt-2">
                <button
                    type="button"
                    onClick={onCancel}
                    className="font-bold uppercase text-red-600"
                >
                    Avbryt
                </button>
                <button
                    type="submit"
                    className="font-bold uppercase text-green-600"
                >
                    Spara
                </button>
            </div>
        </form>
    );
}

type PhoneFormProps = {
    onCancel: () => void;
    onSave: (value: string) => void;
};

function PhoneForm({ onCancel, onSave }: PhoneFormProps) {
    const [phone, setPhone] = useState("");

    const handleSave = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();
        onSave(phone);
    };

    return (
        <form onSubmit={handleSave} className="flex flex-col gap-4 font-montserrat">
            <div>
                <label className="text-sm font-bold text-dark-navy">
                    Telefonnummer
                </label>
                <input
                    type="tel"
                    inputMode="numeric"
                    value={phone}
                    onChange={(e) => setPhone(e.target.value.replace(/\D/g, ""))}
                    placeholder="Ange telefonnummer"
                    className="mt-1 w-full rounded-md border border-nordiska-blue px-3 py-2 placeholder:text-gray-400"
                />
            </div>

            <div className="flex justify-end gap-6 pt-2">
                <button
                    type="button"
                    onClick={onCancel}
                    className="font-bold uppercase text-red-600"
                >
                    Avbryt
                </button>
                <button
                    type="submit"
                    className="font-bold uppercase text-green-600"
                >
                    Spara
                </button>
            </div>
        </form>
    );
}
