import { useState, type ComponentType } from "react";
import { Reorder } from "motion/react";
import { useTranslation } from "react-i18next";
import AccountsCard from "../components/overview/AccountsCard";
import SavingsGoalsCard from "../components/overview/SavingsGoalsCard";
import PlannedTransfersCard from "../components/overview/PlannedTransfersCard";
import RecentTransactionsCard from "../components/overview/RecentTransactionsCard";
import ReorderableCard from "../components/overview/ReorderableCard";
import InboxCard from "../components/overview/InboxCard";
import InterestCard from "../components/overview/InterestCard";
import { useCardOrder, type CardId } from "../components/overview/useCardOrder";
import { useUserStore } from "../store/userStore";
import { useUpdateOverviewLayout } from "../hooks/useUpdateOverviewLayout";

const CARD_COMPONENTS: Record<CardId, ComponentType> = {
    accounts: AccountsCard,
    savings: SavingsGoalsCard,
    planned: PlannedTransfersCard,
    transactions: RecentTransactionsCard,
    inbox: InboxCard,
    interest: InterestCard
};

export default function OverviewPage() {
    const { t } = useTranslation();
    const { order, setOrder, beginEditing, move, reset } = useCardOrder();
    const updateOverviewLayout = useUpdateOverviewLayout();
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

    function handleStartEditing() {
        beginEditing();
        setIsEditing(true);
        setAnnouncement("");
    }

    async function handleDone() {
        try {
            await updateOverviewLayout.mutateAsync(order);
            setIsEditing(false);
            setAnnouncement(t("overview-route.edit-done"));
        } catch (error) {
            console.error("Could not save overview layout", error);
            setAnnouncement(t("overview-route.edit-save-error"));
        }
    }

    return (
        <div className="flex flex-col items-center flex-1 w-full bg-nordiska-bg px-3 py-5 sm:px-6 sm:py-8 xl:max-w-7xl mx-auto">
            <div className="flex items-center gap-3 justify-bewteen mx-auto w-full sm:px-6 sm:max-w-lg lg:max-w-4xl xl:max-w-5xl mb-2">
                <p id="overview-edit-instructions" className="min-w-0 flex-1 text-xs text-secondary md:text-sm">{isEditing ? t("overview-route.edit-instructions") : t("overview-route.viewing-instruction")}</p>
                <div className={`flex shrink-0 items-center gap-2 ${isEditing ? "mb-5" : ""}`}>
                    {isEditing && (
                        <button
                            type="button"
                            onClick={handleReset}
                            className="group flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-lg border border-secondary/40 px-0 py-0 text-xs uppercase tracking-wider text-secondary/70 shadow-md transition hover:bg-primary-blue hover:text-white sm:h-7 sm:w-auto sm:justify-start sm:px-2"
                        >
                            <span aria-hidden="true" className="block size-4 bg-secondary/50 mask-[url('/icons/reset.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-white" />
                            <span className="hidden md:inline sm:ml-1.5">{t("overview-route.edit-reset")}</span>
                        </button>
                    )}

                    <button
                        type="button"
                        onClick={isEditing ? handleDone : handleStartEditing}
                        disabled={updateOverviewLayout.isPending}
                        aria-pressed={isEditing}
                        className={`group flex size-8 shrink-0 cursor-pointer items-center justify-center rounded-lg px-0 py-0 text-xs uppercase tracking-wider shadow-md transition sm:h-7 sm:w-auto sm:justify-start sm:px-2 ${isEditing ? "border border-primary-blue bg-primary-blue text-white hover:bg-white/70 hover:text-primary-blue" : "border border-secondary/40 text-secondary/70 hover:bg-primary-blue hover:text-white"}`}
                    >
                        <span
                            aria-hidden="true"
                            className={`block size-4 mask-contain mask-center mask-no-repeat sm:size-[18px] md:mr-1.5 ${isEditing ? "bg-white group-hover:bg-primary-blue" : "bg-secondary/50 group-hover:bg-white"}`}
                            style={{maskImage: `url('/icons/${isEditing ? "done" : "grip"}.svg')`,}}
                        />
                        <span className="hidden md:inline"> {isEditing ? t("overview-route.edit-done") : t("overview-route.edit-start")} </span>
                    </button>
                </div>
            </div>

            <div className="mx-auto ">
                <p aria-live="polite" className="sr-only"> {announcement} </p>

                <Reorder.Group
                    values={order}
                    onReorder={isEditing ? setOrder : () => {}}
                    className="grid grid-cols-1 gap-4 sm:grid-cols-2 sm:gap-5 lg:grid-cols-3 lg:gap-6"
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