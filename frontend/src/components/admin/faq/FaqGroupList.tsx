import type { Faq, FaqGroup } from "../../../types/types";


type Orders = "asc" | "desc" | undefined;

type FaqGroupListProps = {
    faqGroups: FaqGroup[];
    actionLabel?: string;
    actionBtnStyle?: "edit" | "delete"
    onAction?: (relationId: string) => void;
    sortByHelpful?: boolean;
    sortingOrder?: Orders;
};

// ---------------------------------------
// VISA FAQ GRUPPERADE på RELATIONS ID
// HANTERAR VALD ACTION: edit eller delete
// ---------------------------------------
//

function sortByHelpfulCount(faqs: Faq[], order: Orders) {
    const dir = order === "asc" ? 1 : -1;
    return faqs.toSorted((a, b) => dir * ((a.helpfulCount ?? 0) - (b.helpfulCount ?? 0)));
}

export default function FaqGroupList({
    faqGroups,
    actionLabel,
    actionBtnStyle,
    onAction,
    sortByHelpful = false,
    sortingOrder
}: FaqGroupListProps) {

    const color = actionBtnStyle === "edit" ? "yellow-500" : "red-500";

    return (
        <div className="mt-4 space-y-3 w-[80%] mx-auto">
            {faqGroups.map((group) => (
                <div
                    key={group.relationId}
                    className="border rounded-lg p-4"
                >
                    <div className="grid grid-cols-2 gap-6">
                        {(sortByHelpful ? sortByHelpfulCount(group.faqs, sortingOrder) : group.faqs).map((faq: Faq) => (
                            <div key={faq.id}>
                                {/* Spårk */}
                                <small>{faq.lang === "sv" ? "Svenska" : "English"}</small>
                                {/* Relations ID */}
                                <p className="text-sm"> <strong>Relations ID: </strong> {faq.relationId}</p>
                                {/* Fråga */}
                                <h3 className="font-semibold bg-black text-white p-1 mt-1">{faq.question}</h3>
                                {/* Svar */}
                                <p className="bg-gray-200 p-1">{faq.answer}</p>
                                {/* Kategori */}
                                <p className="mt-2 text-sm"><strong>Kategori: </strong>{faq.category}</p>
                                {/* Keywords */}
                                <p className="mt-2 text-sm"><strong>Keywords: </strong>{faq.keywords?.join(", ")}</p>
                                {/* Faq ID */}
                                <p className="mt-2 text-sm"><strong>ID: </strong>{faq.id}</p>
                                {/* Faq helpful */}
                                <p className="mt-2 text-sm"><strong>Betyg: </strong>{faq.helpfulCount}</p>
                                {}
                            </div>
                        ))}
                    </div>

                    {onAction && (
                        <div className="flex justify-end mt-4">
                            <button
                                type="button"
                                onClick={() => onAction(group.relationId)}
                                className={`border-5 bg-${color} border-black rounded-md px-4 py-2 cursor-pointer font-semibold hover:bg-white hover:text-black hover:border-${color}`}
                            >
                                {actionLabel}
                            </button>
                        </div>
                    )}
                </div>
            ))}
        </div>
    );
}


