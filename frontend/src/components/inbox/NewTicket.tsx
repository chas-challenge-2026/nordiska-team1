import { useEffect, useId, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import Modal from "../modals/Modal";
import { useCreateSupportTicket } from "../../hooks/useInbox";
// ENDPOINT VERSION: replace import above with this one.
// import { useCreateSupportTicket, useTicketCategories } from "../../hooks/useInbox";

type NewTicketButtonProps = {
    /** Optional: called with new thread id after ticket is created. */
    onCreated?: (threadId: number) => void;
    className?: string;
};

const btn = "cursor-pointer rounded bg-primary-blue px-4 py-2 font-semibold text-white disabled:cursor-default disabled:opacity-40";
const field = "w-full border border-secondary p-2";
const label = "text-xs font-semibold uppercase text-secondary";

// Temporary: hardcoded until faqs/{language}/categories works.
// Values are sent to backend as-is; labels come from inbox.ticket.categories.
// ENDPOINT VERSION: delete this constant.
const CATEGORIES = ["Sparkonto", "Sparmål", "Skatteunderlag", "Allmänt"];

export default function NewTicketButton({ onCreated, className = btn }: NewTicketButtonProps) {
    const { t } = useTranslation();
    const [isOpen, setIsOpen] = useState(false);

    return (
        <>
            <button type="button" className={className} onClick={() => setIsOpen(true)}>
                <span aria-hidden="true">+ </span>{t("inbox.ticket.new")}
            </button>

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
    // ENDPOINT VERSION: replace line above with these two.
    // const { t, i18n } = useTranslation();
    // const categories = useTicketCategories(i18n.language);
    const id = useId();
    const subjectRef = useRef<HTMLInputElement>(null);
    const doneRef = useRef<HTMLHeadingElement>(null);
    const create = useCreateSupportTicket();
    const [subject, setSubject] = useState("");
    const [category, setCategory] = useState("");
    const [body, setBody] = useState("");
    const [showRequired, setShowRequired] = useState(false);

    const valid = subject.trim() !== "" && category.trim() !== "" && body.trim() !== "";

    // Dialog focuses its close button first; move focus to first field.
    useEffect(() => {
        subjectRef.current?.focus();
    }, []);

    // Move focus to confirmation so it is announced.
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
                    <button type="button" onClick={onClose} className={btn}>
                        {t("inbox.ticket.close")}
                    </button>
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

            {/* HARDCODED VERSION: delete this select when endpoint works. */}
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

            {/* ENDPOINT VERSION: uncomment this block.
            <select
                id={`${id}-category`}
                required
                value={category}
                onChange={(event) => setCategory(event.target.value)}
                disabled={categories.isPending || categories.isError}
                className={field}
            >
                <option value="">
                    {categories.isPending ? t("inbox.loading") : t("inbox.ticket.select-category")}
                </option>
                {categories.data?.map((name) => (
                    <option key={name} value={name}>{name}</option>
                ))}
            </select>
            {categories.isError && (
                <span role="alert" className="text-error">
                    {t("inbox.error.load")}{" "}
                    <button type="button" onClick={() => categories.refetch()} className="cursor-pointer text-primary-blue underline">
                        {t("inbox.try-again")}
                    </button>
                </span>
            )}
            */}

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
            {create.isError && <span role="alert" className="text-error">{t("inbox.error.action")}</span>}

            <div className="mt-2 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <button type="button" onClick={onClose} className="cursor-pointer px-4 py-2 text-primary-blue underline">
                    {t("inbox.ticket.cancel")}
                </button>
                <button type="submit" className={btn} disabled={create.isPending}>
                    {create.isPending ? t("inbox.sending") : t("inbox.send")}
                </button>
            </div>
        </form>
    );
}
