import { useState } from "react"
import { useNavigate } from "react-router";
import CreateFaq from "../../components/admin/CreateFaq";


export default function FaqAdmin() {
    const navigate = useNavigate()
    const [showMain, setShowMain] = useState<"create" | "edit" | "delete" | "">("");



    return (
        <main className="grid grid-cols-[220px_1fr] min-h-screen">
                    
        <nav className="flex flex-col border-r border-gray-300 p-4">
            <button onClick={() => navigate("/admin")}
                className="border p-4 m-2 mb-15 cursor-pointer hover:bg-light-gray">Tillbaka</button>
            <button onClick={() => setShowMain("create")}
                className={`${showMain === "create" ? "bg-light-gray" : "bg-white"} border-2 p-4 m-2 cursor-pointer hover:bg-light-gray`}>
                    Skapa ny FAQ</button>
            <button onClick={() => setShowMain("edit")}
                className={`${showMain === "edit" ? "bg-light-gray" : "bg-white"} border-2 p-4 m-2 cursor-pointer hover:bg-light-gray`}>
                    Redigera FAQ</button>
            <button onClick={() => setShowMain("delete")}
                className={`${showMain === "delete" ? "bg-light-gray" : "bg-white"} border-2 border-red-500 p-4 m-2 cursor-pointer hover:bg-light-gray`}>
                    Radera FAQ</button>
        </nav>
        <div className="p-8">

            {showMain === "create" && (
                <>
                <h1 className="text-3xl underline underline-offset-10 font-semibold">Skapa ny FAQ</h1>
                <CreateFaq/>
                
                </>
                
            )}

            {showMain === "edit" && (
                <>
                <h1 className="text-3xl underline underline-offset-10 font-semibold">Redigera FAQ</h1>
                <form action="">

                </form>
                
                </>
                
            )}

            {showMain === "delete" && (
                <>
                <h1 className="text-3xl underline underline-offset-10 font-semibold">Radera FAQ</h1>
                <form action="">

                </form>
                
                </>
                
            )}

            

        </div>
        </main>
    )
}