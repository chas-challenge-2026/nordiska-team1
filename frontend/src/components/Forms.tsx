import { useState } from "react";
import type { SubmitEvent } from "react";
import InputField from "./InputField";
import { CollapsibleFormBtns } from "./Buttons";

// -------------------------
// COLLAPSIBLE FUNCTION PROP
// -------------------------
type CollapsibleFormProps = {
    onClose: () => void;
};

// ---------------------
// ADD/CHANGE EMAIL FORM
// ---------------------
export function EmailForm({ onClose }: CollapsibleFormProps) {
    const [email, setEmail] = useState("");
    const [confirmEmail, setConfirmEmail] = useState("");
    const [error, setError] = useState("");

    const handleSubmit = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();

        if (email.trim().toLowerCase() !== confirmEmail.trim().toLowerCase()) {
            setError("Email matchar inte");
            return;
        }

        setError("");

        // UPPDATERA DB HÄR?
        // GLÖM EJ .trim() på email innan save (trimmedEmail?)

        setEmail("");
        setConfirmEmail("");

        // User Feedback att det är sparat???

        onClose();
    }

    return (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4 font-montserrat">
            <InputField 
                name = "email"
                type = "email"
                label = "email"
                placeholder= "ange email"
                value={email}
                required
                onChange={setEmail}
            />

            <InputField 
                name = "emailConfirmation"
                type = "email"
                label = "upprepa email"
                placeholder= "upprepa email"
                value={confirmEmail}
                required
                onChange={setConfirmEmail}
                error={error}
            />

            <CollapsibleFormBtns onClose={onClose} />
        </form>
    );
};

// ---------------------
// ADD/CHANGE PHONE FORM
// ---------------------
export function PhoneForm({ onClose }: CollapsibleFormProps) {
    const [phone, setPhone] = useState("");
    const [error, setError] = useState("");

    const handleSubmit = (e: SubmitEvent<HTMLFormElement>) => {
        e.preventDefault();

        const trimmedPhone = phone.trim();

        setError("");

        if (!/^07\d{8}$/.test(trimmedPhone)) {
            setError("Ange enligt format 07xxxxxxxx");
            return;
        }

        // UPPDATERA DB HÄR (trimmedPhone)

        setPhone("");

        // User feedback

        onClose();
    }

    return (
        <form onSubmit={handleSubmit} className="flex flex-col gap-4 font-montserrat">
            <InputField 
                name = "phone"
                type = "tel"
                label = "phone"
                placeholder= "ange telefonnummer"
                value={phone}
                required
                onChange={setPhone}
                error={error}
            />

            <CollapsibleFormBtns onClose={onClose} />
        </form>
    );
};