import { NavLink, Outlet, useNavigate } from "react-router";
import { adminButtonClass } from "./adminButtonStyles";

export type AdminNavItem = {
    to: string;
    label: string;
    end?: boolean;
    danger?: boolean;
};

type Props = {
    title: string;
    items: AdminNavItem[];
};

export default function AdminLayout({ title, items }: Props) {
    const navigate = useNavigate();

    return (
        <main className="grid grid-cols-[220px_1fr] min-h-screen font-inter">
            <nav className="flex flex-col gap-3 border-r border-gray-300 p-4">
                <button
                    type="button"
                    onClick={() => navigate("/admin")}
                    className={adminButtonClass("neutral", "mb-10")}
                >
                    Tillbaka
                </button>

                <h2 className="text-3xl underline underline-offset-8 text-center mb-2 font-bold">{title}</h2>

                {items.map((item) => (
                    <NavLink
                        key={item.to}
                        to={item.to}
                        end={item.end}
                        className={({ isActive }) =>
                            `${adminButtonClass("neutral", item.danger ? "!border-red-500" : "")} ${isActive ? "!bg-gray-400" : ""}`.trim()
                        }
                    >
                        {item.label}
                    </NavLink>
                ))}
            </nav>
            <div className="min-w-0 p-4 mt-2">
                <Outlet />
            </div>
        </main>
    );
}
