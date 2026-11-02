import { useNavigate, useSearchParams } from "react-router";
import AdminThreads from "./AdminThreads";
import AdminBroadcast from "./AdminBroadcast";
import AdminPublishTerms from "./AdminPublishTerms";
import AdminButton from "../../../components/admin/ui/AdminButton";
import { AdminPageTitle } from "../../../components/admin/ui/AdminTitle";

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
            <header className="flex flex-col gap-3">
                <AdminPageTitle>Kommunikationscenter</AdminPageTitle>
                <div>
                    <AdminButton onClick={() => navigate("/admin")}>Tillbaka</AdminButton>
                </div>
            </header>

            <nav aria-label="Sektioner" className="flex flex-wrap gap-2">
                {SECTIONS.map((s) => (
                    <AdminButton
                        key={s.key}
                        aria-current={s.key === active ? "page" : undefined}
                        onClick={() => setSearchParams({ section: s.key })}
                        className={s.key === active ? "!bg-gray-400" : ""}
                    >
                        {s.label}
                    </AdminButton>
                ))}
            </nav>

            {active === "threads" && <AdminThreads />}
            {active === "broadcast" && <AdminBroadcast />}
            {active === "terms" && <AdminPublishTerms />}
        </main>
    );
}
