import { useNavigate } from "react-router"

export default function Admin(){

    const navigate = useNavigate();

    return (
        <main>

            <header className="p-5 flex justify-between items-center border-b-2">
                <div>
                    <h1 className="text-2xl">ADMIN</h1>
                    <p>Inloggad som: user345</p>
                </div>
                <div>
                    <button
                        className="border p-1 cursor-pointer hover:bg-light-gray font-semibold">
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
                <button 
                    className="border p-4 m-2 cursor-pointer hover:bg-light-gray font-semibold"
                >
                        Se hjälpcenter statistik
                </button>
                <button 
                    className="border p-4 m-2 cursor-pointer hover:bg-light-gray font-semibold"
                >
                        ?????
                </button>
            </nav>

        </main>
    )
}