import { useState, type FormEvent } from "react";
import { usePublishNewTerms } from "../../../hooks/inbox/useStaffInbox";
import { input, section } from "./adminStyles";
import AdminButton from "../../../components/admin/ui/AdminButton";
import AdminStatus from "../../../components/admin/ui/AdminStatus";
import { AdminSectionTitle } from "../../../components/admin/ui/AdminTitle";

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
            <AdminSectionTitle>Publicera villkor</AdminSectionTitle>

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
                <AdminButton type="submit" variant="green" className="self-start" disabled={publish.isPending}>
                    {publish.isPending ? "Publicerar..." : "Publicera"}
                </AdminButton>
                {publish.isSuccess && (
                    <p role="status">
                        Villkor publicerade: {publish.data.title} v{publish.data.version} (ID {publish.data.id}).
                    </p>
                )}
                {publish.isError && <AdminStatus kind="error">Kunde inte publicera villkor.</AdminStatus>}
            </form>
        </section>
    );
}
