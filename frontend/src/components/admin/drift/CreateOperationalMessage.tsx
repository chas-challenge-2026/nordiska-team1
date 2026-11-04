
import { useNavigate } from "react-router";
import OperationalMessageForm from "./OperationalMessageForm";
import { AdminPageTitle } from "../ui/AdminTitle";

export default function CreateOperationalMessage() {
    const navigate = useNavigate();

    return (
    <>
        <AdminPageTitle>Skapa driftmeddelande</AdminPageTitle>
        <section className="mt-10 space-y-4">
            <OperationalMessageForm
                onCancel={() => navigate("/admin/operational-message")}
                onSaved={() => navigate("/admin/operational-message")}
            />
        </section> 
    </>
    );
}
