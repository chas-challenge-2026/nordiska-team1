import { useState, type FormEvent } from "react";
import { usePublishNewTerms } from "../../../hooks/inbox/useStaffInbox";
import { btn, input, section } from "./adminStyles";

export default function AdminPublishTerms() {
    const publish = usePublishNewTerms();
    const [code, setCode] = useState("");
    const [title, setTitle] = useState("");
    const [version, setVersion] = useState("");
    const [documentId, setDocumentId] = useState("");
    const [effectiveFrom, setEffectiveFrom] = useState("");

    const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
        event.preventDefault();
        // Publishing terms reaches customers; ask first.
        if (!window.confirm(`Publicera "${title}" version ${version}?`)) return;

        publish.mutate(
            {
                code: code.trim(),
                title: title.trim(),
                version: Number(version),
                documentId: Number(documentId),
                effectiveFrom: new Date(effectiveFrom).toISOString(),
            },
            {
                onSuccess: () => {
                    setCode("");
                    setTitle("");
                    setVersion("");
                    setDocumentId("");
                    setEffectiveFrom("");
                },
            },
        );
    };

    return (
        <section className={section}>
            <h2 className="text-xl font-semibold">Publicera villkor</h2>

            <form onSubmit={handleSubmit} className="flex flex-col gap-2 max-w-[600px]">
                <label className="flex flex-col">
                    Kod
                    <input className={input} required value={code} onChange={(e) => setCode(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Titel
                    <input className={input} required value={title} onChange={(e) => setTitle(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Version
                    <input className={input} type="number" min={1} required value={version} onChange={(e) => setVersion(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Dokument-ID
                    <input className={input} type="number" min={1} required value={documentId} onChange={(e) => setDocumentId(e.target.value)} />
                </label>
                <label className="flex flex-col">
                    Gäller från
                    <input className={input} type="date" required value={effectiveFrom} onChange={(e) => setEffectiveFrom(e.target.value)} />
                </label>
                <button type="submit" className={`${btn} self-start`} disabled={publish.isPending}>
                    {publish.isPending ? "Publicerar..." : "Publicera"}
                </button>
                {publish.isSuccess && (
                    <p role="status">
                        Villkor publicerade: {publish.data.title} v{publish.data.version} (ID {publish.data.id}).
                    </p>
                )}
                {publish.isError && <p role="alert" className="text-error">Kunde inte publicera villkor.</p>}
            </form>
        </section>
    );
}
