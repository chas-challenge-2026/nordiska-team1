import AdminLayout from "../../components/admin/ui/AdminLayout";

const items = [
    { to: "/admin/operational-message", label: "Aktuella", end: true },
    { to: "/admin/operational-message/create", label: "Skapa nytt" },
];

export default function OperationalMessage() {
    return <AdminLayout title="DRIFT" items={items} />;
}
