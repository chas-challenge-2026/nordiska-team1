import { useNavigate, Navigate } from "react-router"
import { useLogin } from "../../hooks/useLogin";
import { useLogout } from "../../hooks/useLogout";
import { useUserStore } from "../../store/userStore";
import InputField from "../../components/forms/InputField";
import AdminButton from "../../components/admin/ui/AdminButton";
import { AdminPageTitle } from "../../components/admin/ui/AdminTitle";
import AdminStatus from "../../components/admin/ui/AdminStatus";
import { useState } from "react";
import { isAxiosError } from "axios";

export default function Admin(){
    const navigate = useNavigate();
    const user = useUserStore((state) => state.user);
    const setUser = useUserStore((state) => state.setUser);
    const clearUser = useUserStore((state) => state.logout);

    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [emailError, setEmailError] = useState<string>();
    const [notAuthorized, setNotAuthorized] = useState("");
    
    const { mutate: loginUser, isPending } = useLogin();
    const { mutate } = useLogout();

    if (user?.role === "Customer") {
        return <Navigate to="/" replace />;
    }

    function handleLogout() {
        mutate(undefined, {
            onSuccess: () => {
                clearUser("notAuthorized");
            },
        });
    }

    function handleLogin(e: React.SubmitEvent<HTMLFormElement>) {
        e.preventDefault();

        setEmailError(undefined);
        setNotAuthorized("");

        loginUser(
            {email, password},
            {
                onSuccess: (user) => {
                    if (!user) {return;}
                    if (user?.role !== "Admin") {
                        setNotAuthorized(user.email);
                        handleLogout();
                        return;
                    }
                    setUser(user);
                },
                onError: (err) => {
                    const code = isAxiosError(err) ? err.response?.status : undefined;
                    if (code === 401) { setEmailError("Fel e-post eller lösenord."); }
                    else if (code === 423) { setEmailError("Kontot är tillfälligt spärrat. Försök igen senare."); } 
                    else if (code === 429) { setEmailError("För många försök. Vänta en stund och försök igen."); } 
                    else { setEmailError("Något gick fel. Försök igen senare."); }
                },
            }
        );
    }

    const isAdmin = user?.role === "Admin";

    return (
        <main className="font-inter">
            {!isAdmin ? (
            <>
                <div >
                    <AdminPageTitle className="mb-10">ADMIN</AdminPageTitle>
                    { notAuthorized &&
                        <div className="w-[500px] mx-auto mb-10">
                            <div className="border-8 border-red-500 bg-yellow-500 p-2">
                                <p className="font-semibold text-lg">BEHÖRIGHET SAKNAS FÖR ANVÄNDARE:</p>
                                <p>{notAuthorized}</p>
                            </div>

                            <AdminButton onClick={() => navigate("/")} className="mt-5 block w-[350px] mx-auto text-sm">
                                Tillbaka till Kundportalen / Back to Customer Portal
                            </AdminButton>
                        </div>
                    }

                    <form onSubmit={handleLogin} className="flex flex-col gap-3 w-[350px] mx-auto">
                        <InputField
                            name="email"
                            type="email"
                            label="E-post"
                            placeholder="Ange e-post"
                            value={email}
                            onChange={setEmail}
                            required
                        />
                        <InputField
                            name="password"
                            type="password"
                            label="Lösenord"
                            placeholder="Ange lösenord"
                            value={password}
                            onChange={setPassword}
                            required
                        />
                        <AdminButton type="submit" variant="green" disabled={isPending}>
                            {isPending ? "Loggar in..." : "Logga in"}
                        </AdminButton>
                        {emailError && <AdminStatus kind="error">{emailError}</AdminStatus>}
                    </form>
                </div>
            </>
            ) : (
            <>
            <AdminPageTitle>ADMIN</AdminPageTitle>
            <header className="p-5 flex justify-between items-center">
                <p>Inloggad som: {user?.email ? user?.email : "Admin"}</p>
                <AdminButton onClick={() => {handleLogout();}}>Logga ut</AdminButton>
            </header>

            <nav className="flex w-full mt-10 justify-center items-center gap-4">
                <AdminButton onClick={() => navigate("faq")}>Hantera FAQ</AdminButton>
                <AdminButton onClick={() => navigate("operational-message")}>Driftmeddelande</AdminButton>
                <AdminButton onClick={() => navigate("inbox")}>Kommunikationscenter</AdminButton>
            </nav>
            </>
            )}
        </main>
    );
}
