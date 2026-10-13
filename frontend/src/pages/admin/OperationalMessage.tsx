
import { NavLink, Outlet, useNavigate } from "react-router";

export default function OperationalMessage() {
    const navigate = useNavigate();

    return (
        <main className="grid min-h-screen grid-cols-[220px_1fr] font-inter">
            <nav className="flex flex-col border-r border-gray-300 p-4">
                <button
                    type="button"
                    onClick={() => navigate("/admin")}
                    className="m-2 mb-15 cursor-pointer rounded-md border p-4 hover:bg-gray-400"
                >
                    Tillbaka
                </button>

                <h2 className="mb-5 text-center text-3xl font-bold underline underline-offset-8">DRIFT</h2>

                <NavLink
                    end
                    to="/admin/operational-message"
                    className={({ isActive }) => `m-2 cursor-pointer rounded-md border-5 p-4 text-center font-semibold ${isActive ? "bg-gray-400" : "bg-white"} hover:bg-gray-400`}
                >
                    Aktuella
                </NavLink>

                <NavLink
                    to="/admin/operational-message/create"
                    className={({ isActive }) => `m-2 cursor-pointer rounded-md border-5 p-4 text-center font-semibold ${ isActive ? "bg-gray-400" : "bg-white"} hover:bg-gray-400`}
                >
                    Skapa nytt
                </NavLink>
            </nav>

            <div className="mt-2 min-w-0 p-4">
                <Outlet />
            </div>
        </main>
    );
}
