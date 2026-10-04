import { useNavigate } from "react-router"
import { useLogin } from "../../hooks/useLogin";
import { useLogout } from "../../hooks/useLogout";
import { useUserStore } from "../../store/userStore";
import InputField from "../../components/forms/InputField";
import { useState } from "react";
import { isAxiosError } from "axios";

export default function Admin(){
    const navigate = useNavigate();

    const [email, setEmail] = useState("");
    const [password, setPassword] = useState("");
    const [emailError, setEmailError] = useState<string>();
    const [notAuthorized, setNotAuthorized] = useState("");
    
    const { mutate: loginUser, isPending } = useLogin();
    const { mutate } = useLogout();

    const user = useUserStore((state) => state.user);
    const setUser = useUserStore((state) => state.setUser);
    const clearUser = useUserStore((state) => state.logout);

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

    return (
        <main className="font-inter">
            {!user ? (
            <>
                <div className="mt-10">
                    <h1 className="text-3xl text-center font-semibold mb-10">ADMIN</h1>

                    { notAuthorized &&
                        <div className="w-[500px] mx-auto mb-10">
                            <div className="border-8 border-red-500 bg-yellow-500 p-2">
                                <p className="font-semibold text-lg">BEHÖRIGHET SAKNAS FÖR ANVÄNDARE:</p>
                                <p>{notAuthorized}</p>
                            </div>

                            <button onClick={() => navigate("/")}
                                className="mt-5 block w-[350px] mx-auto bg-primary-blue p-2 mx-auto text-white text-sm hover:bg-nordiska-blue cursor-pointer">
                                Tillbaka till Kundportalen / Back to Customer Portal
                            </button>
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
                        <button type="submit" disabled={isPending} className="bg-primary-blue text-white p-1 cursor-pointer">
                            {isPending ? "Loggar in..." : "Logga in"}
                        </button>
                        {emailError && <p className="text-sm text-error">{emailError}</p>}
                    </form>
                </div>
            </>
            ) : (
            <>
            <header className="p-5 flex justify-between items-center border-b-2">
                <div>
                    <h1 className="text-2xl">ADMIN</h1>
                    <p>Inloggad som: {user?.email ? user?.email : "Admin"}</p>
                </div>
                <div>
                    <button
                        onClick={() => {handleLogout();}}
                        className="border py-1 px-2 cursor-pointer hover:bg-light-gray font-semibold">
                            Logga ut
                    </button>
                </div>
            </header>

            <nav className="flex w-full mt-10 justify-center items-center">
                <button 
                    onClick={() => navigate("faq")}
                    className="border p-4 m-2 cursor-pointer hover:bg-light-gray font-semibold"
                >
                        Hantera FAQ
                </button>
                <button className="border p-4 m-2 cursor-pointer hover:bg-light-gray font-semibold">
                        Se hjälpcenter statistik
                </button>
                <button className="border p-4 m-2 cursor-pointer hover:bg-light-gray font-semibold">
                        ?????
                </button>
            </nav>
            </>
            )}
        </main>
    );
}