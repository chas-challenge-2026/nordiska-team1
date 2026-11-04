import AdminLayout from "../../../components/admin/ui/AdminLayout";

const items = [
    { to: "/admin/faq/create", label: "Skapa ny" },
    { to: "/admin/faq/edit", label: "Redigera" },
    { to: "/admin/faq/stats", label: "Statistik" },
    { to: "/admin/faq/delete", label: "Radera", danger: true },
];

export default function FaqAdmin() {
    return <AdminLayout title="FAQ" items={items} />;
}
