import { useNavigate, useSearchParams } from "react-router";
import AdminThreads from "./AdminThreads";
import AdminBroadcast from "./AdminBroadcast";
import AdminPublishTerms from "./AdminPublishTerms";
import { btn } from "./adminStyles";

const SECTIONS = [
    { key: "threads", label: "Trådar" },
    { key: "broadcast", label: "Skicka meddelande" },
    { key: "terms", label: "Publicera villkor" },
] as const;

type SectionKey = (typeof SECTIONS)[number]["key"];

export default function AdminInbox() {
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();

    const param = searchParams.get("section");
    const active: SectionKey = SECTIONS.some((s) => s.key === param) ? (param as SectionKey) : "threads";

    return (
        <main className="font-inter p-5 flex flex-col gap-6">
            <header className="flex justify-between items-center border-b-2 pb-3">
                <h1 className="text-2xl">Kommunikationscenter</h1>
                <button type="button" onClick={() => navigate("/admin")} className={btn}>
                    Tillbaka
                </button>
            </header>

            <nav aria-label="Sektioner" className="flex flex-wrap gap-2">
                {SECTIONS.map((s) => (
                    <button
                        key={s.key}
                        type="button"
                        aria-current={s.key === active ? "page" : undefined}
                        onClick={() => setSearchParams({ section: s.key })}
                        className={`${btn} ${s.key === active ? "bg-light-gray" : ""}`}
                    >
                        {s.label}
                    </button>
                ))}
            </nav>

            {active === "threads" && <AdminThreads />}
            {active === "broadcast" && <AdminBroadcast />}
            {active === "terms" && <AdminPublishTerms />}
        </main>
    );
}
