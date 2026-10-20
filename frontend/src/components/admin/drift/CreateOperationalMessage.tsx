
import { useNavigate } from "react-router";
import OperationalMessageForm from "./OperationalMessageForm";

export default function CreateOperationalMessage() {
    const navigate = useNavigate();

    return (
    <>
        <h1 className="text-3xl font-semibold py-2.5 bg-black text-white text-center">Skapa driftmeddelande</h1>
        <section className="mt-10 space-y-4">
            <OperationalMessageForm
                onCancel={() => navigate("/admin/operational-message")}
                onSaved={() => navigate("/admin/operational-message")}
            />
        </section> 
    </>
    );
}
