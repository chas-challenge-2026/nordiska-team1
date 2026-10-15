import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import Modal from "../modals/Modal";
import { useCreateSupportTicket } from "../../hooks/inbox/useThreads";
import type { Categories } from "../../types/inbox";
import InboxButton from "./InboxButton";

type NewTicketButtonProps = {
    /** Optional: called with new thread id after ticket is created. */
    onCreated?: (threadId: number) => void;
    className?: string;
};

const field = "w-full border border-secondary p-2";
const label = "text-xs font-semibold uppercase text-secondary";

const CATEGORIES: Categories[] = ["Sparkonto", "Sparmål", "Skatteunderlag", "Allmänt"];

export default function NewTicketButton({ onCreated, className }: NewTicketButtonProps) {
    const { t } = useTranslation();
    const [isOpen, setIsOpen] = useState(false);

    return (
        <>
            <InboxButton variant="subtle" className={`py-2 ${className ?? ""}`} onClick={() => setIsOpen(true)}>
                {t("inbox.ticket.new")}
            </InboxButton>

            <Modal
                isOpen={isOpen}
                onClose={() => setIsOpen(false)}
                title={t("inbox.ticket.new")}
                header={<h2 className="py-3 pr-10 text-lg font-bold text-dark-navy">{t("inbox.ticket.new")}</h2>}
            >
                <NewTicketForm onCreated={onCreated} onClose={() => setIsOpen(false)} />
            </Modal>
        </>
    );
}

type NewTicketFormProps = {
    onCreated?: (threadId: number) => void;
    onClose: () => void;
};

function NewTicketForm({ onCreated, onClose }: NewTicketFormProps) {
    const { t } = useTranslation();
    const id = useId();
    const subjectRef = useRef<HTMLInputElement>(null);
    const doneRef = useRef<HTMLHeadingElement>(null);
    const create = useCreateSupportTicket();
    const [subject, setSubject] = useState("");
    const [category, setCategory] = useState("");
    const [body, setBody] = useState("");
    const [showRequired, setShowRequired] = useState(false);

    const valid = subject.trim() !== "" && category.trim() !== "" && body.trim() !== "";

    useEffect(() => {
        subjectRef.current?.focus();
    }, []);

    useEffect(() => {
        if (create.isSuccess) doneRef.current?.focus();
    }, [create.isSuccess]);

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        if (!valid) {
            setShowRequired(true);
            return;
        }
        create.mutate(
            { subject: subject.trim(), category: category.trim(), body: body.trim() },
            { onSuccess: (thread) => onCreated?.(thread.id) },
        );
    };

    if (create.isSuccess) {
        return (
            <div className="flex flex-col gap-3 p-4 font-montserrat text-sm text-dark-navy">
                <h3 ref={doneRef} tabIndex={-1} className="text-base font-bold outline-none">
                    {t("inbox.ticket.received-heading")}
                </h3>
                <p>{t("inbox.ticket.received-body", { time: t("inbox.ticket.reply-time") })}</p>
                <div className="mt-2 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                    <Link
                        to={`/inbox/${create.data.id}`}
                        onClick={onClose}
                        className="px-4 py-2 text-center text-primary-blue underline"
                    >
                        {t("inbox.view-message")}
                    </Link>
                    <InboxButton onClick={onClose}>{t("generic.close")}</InboxButton>
                </div>
            </div>
        );
    }

    return (
        <form onSubmit={handleSubmit} className="flex flex-col gap-2 p-4 font-montserrat text-sm text-dark-navy">
            <label htmlFor={`${id}-subject`} className={label}>{t("inbox.subject")}</label>
            <input
                ref={subjectRef}
                id={`${id}-subject`}
                required
                value={subject}
                onChange={(event) => setSubject(event.target.value)}
                className={field}
            />

            <label htmlFor={`${id}-category`} className={label}>{t("inbox.ticket.category")}</label>

            <select
                id={`${id}-category`}
                required
                value={category}
                onChange={(event) => setCategory(event.target.value)}
                className={field}
            >
                <option value="">{t("inbox.ticket.select-category")}</option>
                {CATEGORIES.map((value) => (
                    <option key={value} value={value}>
                        {t(`inbox.ticket.categories.${value}`, { defaultValue: value })}
                    </option>
                ))}
            </select>

            <label htmlFor={`${id}-body`} className={label}>{t("inbox.ticket.message")}</label>
            <textarea
                id={`${id}-body`}
                required
                rows={5}
                value={body}
                onChange={(event) => setBody(event.target.value)}
                className={field}
            />

            {showRequired && !valid && (
                <span role="alert" className="text-error">{t("inbox.error.required-fields")}</span>
            )}
            {create.isError && <span role="alert" className="text-error">{t("generic.error-action")}</span>}

            <div className="mt-2 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <InboxButton variant="link" className="px-4 py-2" onClick={onClose}>
                    {t("generic.cancel")}
                </InboxButton>
                <InboxButton type="submit" disabled={create.isPending}>
                    {create.isPending ? t("generic.sending") : t("generic.send")}
                </InboxButton>
            </div>
        </form>
    );
}
