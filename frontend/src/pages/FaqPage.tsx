import { useState, useEffect} from "react";
import Collapsible from "../components/Collapsible";
import { searchFaqs, type Faq } from "../services/faqService";


// BEHÖVER 

// public record SearchFaqRequest(
//     [param: StringLength(500)]
//     string? SearchTerm = null,
//     [param: StringLength(200)]
//     string? Category = null,
//     [param: StringLength(200)]
//     string? Keyword = null
 
// );

// i "FaqDtos.cs" för att fungera


export default function FaqPage(){


    const CATEGORIES = ["Ränta", "Insättning", "Uttag", "Rapporter", "Villkor", "Konto"]

    const [selectedCategory, setSelectedCategory] = useState("");
    const [searchField, setSearchField] = useState("");


    const [faqs, setFaqs] = useState<Faq[]>([]);
    const [openFaqId, setOpenFaqId] = useState<number | null>(null);
    const [error, setError] = useState("");


    useEffect(() => {

        async function getFaqs() {
            try {
                const data = await searchFaqs({
                    category: selectedCategory || undefined,
                });

                setFaqs(data);
            } catch (error) {
                console.log(error);
                setError("Misslyckades vid hämtning av data");
            }
        }

        getFaqs();
    }, [searchField, selectedCategory]);

 
    return (
        <>
        
        <main className="relative min-h-[calc(100vh-75px)] text-dark-navy ">
            
            <div className="bg-dark-navy pt-10 pb-5">

                <h2 className="text-white text-4xl font-montserrat-alternates text-center font-semibold">Hur kan vi hjälpa dig idag?</h2>
                    <p className="text-white font-montserrat-alternates text-sm text-center mt-2">
                        Sök eller hitta vanliga frågor efter kategori
                    </p>

                <section className="w-[50%] mx-auto">


                    <div className="relative w-full mx-auto my-2">

                        {/* 
                        INGEN SÖKFUNKTION PÅ DETTA ÄN. BARA STATE 
                        Ändrade med hjälp av AI i "FaqRepository.cs" -> rad 85->110 för att den ska söka på individuella ord i strängen och inte hela sammansatta strängen ordagrant.*/}

                        <input
                            type="text"
                            placeholder="Sök"
                            value={searchField}
                            onChange={(e) => setSearchField(e.target.value)}
                            className="w-full bg-white rounded-2xl py-2 pl-3 pr-9"
                        />

                        {searchField && (
                            <button
                                type="button"
                                onClick={() => setSearchField("")}
                                className="absolute right-2 top-1/2 -translate-y-1/2 cursor-pointer text-primary-blue text-2xl hover:text-dark-navy"
                                aria-label="Rensa sökning"
                            >
                                ×
                            </button>
                        )}
                    </div>
                </section>
                

                <section className="flex gap-2 justify-center mt-4">

                    {CATEGORIES.map((category) => (
                    <button
                        key={category}
                        type="button"
                        onClick={() => {
                            setSelectedCategory(category);
                            setOpenFaqId(null);
                        }}
                        className={`cursor-pointer rounded-3xl border-2 px-3 py-1 text-sm hover:text-dark-navy hover:bg-nordiska-orange
                            ${selectedCategory === category
                                    ? "bg-nordiska-orange"
                                    : "border-nordiska-orange"
                            }
                            ${selectedCategory === category
                                    ? "text-dark-navy border-nordiska-orange"
                                    : "text-white"
                            }
                            
                            `}
                    >
                        {category}
                    </button>
                ))}

                <button
                        type="button"
                        onClick={() => {
                            setSelectedCategory("");
                        }}
                        className="cursor-pointer uppercase px-3 py-1 text-sm text-white ml-6"
                    >
                        Rensa
                    </button>


                </section>
                
            </div>

            <div className="bg-light-gray flex p-5 gap-10 h-full">

                

                <div className="p-4 pb-6 flex-2 bg-white shadow-md">


                    <div className="border-b border-nordiska-orange text-sm pb-3 mb-2">
                        {selectedCategory ? (
                            <p >
                                Visar resultat i kategori:{" "}
                                <span className="font-semibold">
                                    {selectedCategory}
                                </span>
                            </p>
                        ) : (
                            <p>
                                Filtrera vanliga frågor efter kategori eller sök efter din fråga i sökfältet
                            </p>
                        )}
                    </div>
                    

                    {error ? (
                        <p className="text-error mt-8 font-semibold mb-2">
                            {error}
                        </p>
                    ) : (
                        faqs.map((faq) => (
                            <Collapsible
                                key={faq.id}
                                title={faq.question ?? ""}
                                isOpen={openFaqId === faq.id}
                                onOpenChange={(open) =>
                                    setOpenFaqId(open ? faq.id : null)
                                }
                            >
                                {() => (
                                    <p>{faq.answer}</p>
                                )}
                            </Collapsible>
                        ))
                    )}



                </div>
                <div className="flex-1">
                    <article className="bg-dark-navy shadow-md p-3 mb-5">
                        <h3 className="text-white">Hittar du inte svaret?</h3>

                    </article>
                    <article className="bg-white shadow-md p-3">
                        <h3>Relaterat</h3>

                        {/* 

                        Här tänkte jag typ:

                        Hur uppdaterar jag mina kontaktuppgifter?
                        -> Länk till kontoinställningar

                        Hur ändrar jag språk?
                        -> Länk till ändra språk?  

                        */}

                    </article>
                </div>

            </div>

            
        </main>
        
        </>
    )
}