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
            <nav className="w-[220px] shrink-0 border-r border-gray-300 p-4 flex flex-col gap-2">
                <button
                    type="button"
                    onClick={() => navigate("/admin")}
                    className={adminButtonClass("neutral", "w-full justify-start")}
                >
                    Tillbaka
                </button>

                <h2 className="mt-5 mb-0.5 text-[13px] font-bold uppercase tracking-[0.1em] text-gray-600 text-left">{title}</h2>

                {items.map((item) => (
                    <NavLink
                        key={item.to}
                        to={item.to}
                        end={item.end}
                        className={({ isActive }) =>
                            adminButtonClass(isActive ? "navActive" : item.danger ? "navDanger" : "neutral", "w-full justify-start")
                        }
                    >
                        {item.label}
                    </NavLink>
                ))}
            </nav>
            <div className="flex-1 min-w-0 p-6 flex flex-col gap-7">
                <Outlet />
            </div>
        </main>
    );
}
