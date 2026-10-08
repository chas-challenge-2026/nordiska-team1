import { useState, type ComponentType } from "react";
import { Reorder } from "motion/react";
import { useTranslation } from "react-i18next";
import AccountsCard from "../components/overview/AccountsCard";
import SavingsGoalsCard from "../components/overview/SavingsGoalsCard";
import PlannedTransfersCard from "../components/overview/PlannedTransfersCard";
import RecentTransactionsCard from "../components/overview/RecentTransactionsCard";
import ReorderableCard from "../components/overview/ReorderableCard";
import { useCardOrder, type CardId } from "../components/overview/useCardOrder";
import { useUserStore } from "../store/userStore";

const CARD_COMPONENTS: Record<CardId, ComponentType> = {
    accounts: AccountsCard,
    savings: SavingsGoalsCard,
    planned: PlannedTransfersCard,
    transactions: RecentTransactionsCard,
};

export default function OverviewPage() {
    const { t } = useTranslation();
    const { order, setOrder, move, reset } = useCardOrder();
    const [isEditing, setIsEditing] = useState(false);
    const [announcement, setAnnouncement] = useState("");

    const isCheckingSession = useUserStore((state) => state.isCheckingSession);
    if (isCheckingSession) {return null;}

    function handleKeyboardMove(id: CardId, delta: -1 | 1) {
        const to = move(id, delta);
        if (to === null) return;

        setAnnouncement(
            t("overview-route.edit-moved", {
                card: t(`overview-route.card-${id}`),
                position: to + 1,
                total: order.length,
            }),
        );
    }

    function handleReset() {
        reset();
        setAnnouncement(t("overview-route.edit-reset-done"));
    }

    function toggleEditing() {
        setIsEditing((prev) => !prev);
        setAnnouncement("");
    }

    return (
        <div className="flex flex-1 w-full bg-nordiska-bg px-3 py-5 sm:px-6 sm:py-8 lg:px-10 lg:py-10">
            <div className="mx-auto w-full sm:px-6 sm:max-w-lg lg:max-w-4xl xl:max-w-5xl">

                {/* REDIGERA LAYOUT  */}
                <div className={`flex flex-wrap items-center justify-between gap-0.5 md:gap-3 px-1 font-montserrat ${isEditing ? "mb-5" : "mb-3"}`}>
                    {/* hjälptext */}
                    <p id="overview-edit-instructions" className="min-w-0 flex-1 text-sm text-secondary sm:text-[1em]">
                        {isEditing ? t("overview-route.edit-instructions") : t("overview-route.viewing-instruction")}
                    </p>
                    {/* knappar */}
                    <div className="flex w-full shrink-0 justify-end gap-2 sm:w-auto sm:gap-3">
                        {isEditing && (
                            <button
                                type="button"
                                onClick={handleReset}
                                className="group flex cursor-pointer items-center rounded-lg border-secondary px-2 py-1 text-xs uppercase tracking-wider text-secondary/70 shadow-md outline-1 outline-secondary/40 transition hover:bg-primary-blue hover:text-white sm:text-sm"
                            ><>
                                <span aria-hidden="true" className="lg:mr-1.5 block h-4 w-4 bg-secondary/50 mask-[url('/icons/reset.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-white sm:h-4 sm:w-4" />
                                <span className="hidden lg:inline">{t("overview-route.edit-reset")}</span>
                            </></button>
                        )}
                        <button
                            type="button"
                            onClick={toggleEditing}
                            aria-pressed={isEditing}
                            className={`group ml-3 sm:ml-0 flex cursor-pointer items-center rounded-lg px-2 py-1 text-xs uppercase tracking-wider shadow-md transition sm:text-sm
                                ${isEditing
                                    ? "border border-primary-blue bg-primary-blue text-white hover:bg-white/70 hover:text-primary-blue"
                                    : "border border-secondary/40 bg-0 text-secondary/70 hover:bg-primary-blue hover:text-white"
                                }`}
                        >
                            {isEditing
                                ? <>
                                <span aria-hidden="true" className="lg:mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/done.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-primary-blue sm:h-4.5 sm:w-4.5" />
                                <span className="hidden lg:inline">{t("overview-route.edit-done")}</span>
                                </>
                                : <>
                                <span aria-hidden="true" className="lg:mr-1.5 block h-4 w-4 bg-secondary/50 mask-[url('/icons/grip.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-white sm:h-4.5 sm:w-4.5" />
                                <span className="hidden lg:inline">{t("overview-route.edit-start")}</span>
                                </>
                            }
                        </button>
                    </div>
                </div>

                <p aria-live="polite" className="sr-only"> {announcement} </p>

                <Reorder.Group
                    values={order}
                    onReorder={setOrder}
                    className="grid grid-cols-1 gap-4 sm:gap-5 lg:grid-cols-2 md:max-w-lg lg:max-w-5xl md:mx-auto lg:gap-6"
                >
                    {order.map((id) => {
                        const Card = CARD_COMPONENTS[id];
                        return (
                            <ReorderableCard
                                key={id}
                                id={id}
                                label={t(`overview-route.card-${id}`)}
                                isEditing={isEditing}
                                onKeyboardMove={handleKeyboardMove}
                            >
                                <Card />
                            </ReorderableCard>
                        );
                    })}
                </Reorder.Group>
            </div>
        </div>
    );
}