import { NavLink, Outlet, useNavigate } from "react-router";

export default function FaqAdmin() {
    const navigate = useNavigate();

    return (
        <main className="grid grid-cols-[220px_1fr] min-h-screen font-inter">
            <nav className="flex flex-col border-r border-gray-300 p-4">
                <button
                    onClick={() => navigate("/admin")}
                    className="border rounded-md p-4 m-2 mb-15 cursor-pointer hover:bg-gray-400"
                >
                    Tillbaka
                </button>

                <h2 className="text-3xl underline underline-offset-8 text-center mb-5 font-bold">FAQ</h2>

                <NavLink
                    to="/admin/faq/create"
                    className={({ isActive }) =>`font-semibold border-5 rounded-md p-4 m-2 cursor-pointer text-center ${isActive ? "bg-gray-400" : "bg-white"} hover:bg-gray-400`}
                >
                    Skapa ny
                </NavLink>
                <NavLink 
                    to="/admin/faq/edit"
                    className={({ isActive }) =>`font-semibold border-5 rounded-md p-4 m-2 cursor-pointer text-center ${isActive ? "bg-gray-400" : "bg-white"} hover:bg-gray-400`}
                >
                    Redigera
                </NavLink>
                <NavLink to="/admin/faq/delete"
                    className={({ isActive }) => `font-semibold border-5 rounded-md p-4 m-2 cursor-pointer border-red-500 text-center ${isActive ? "bg-gray-400" : "bg-white"} hover:bg-gray-400`}
                >
                    Radera
                </NavLink>
            </nav>
            <div className="p-4 mt-2">
                <Outlet />
            </div>
        </main>
    );
}