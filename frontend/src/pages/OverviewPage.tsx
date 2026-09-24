import { useState, type ComponentType } from "react";
import { Reorder } from "motion/react";
import { useTranslation } from "react-i18next";
import AccountsCard from "../components/overview/AccountsCard";
import SavingsGoalsCard from "../components/overview/SavingsGoalsCard";
import PlannedTransfersCard from "../components/overview/PlannedTransfersCard";
import RecentTransactionsCard from "../components/overview/RecentTransactionsCard";
import ReorderableCard from "../components/overview/ReorderableCard";
import { useCardOrder, type CardId } from "../components/overview/useCardOrder";

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
        <div className="min-h-screen w-full bg-white px-4 py-8 sm:px-8">
            <div className="mx-auto max-w-7xl">
                <div className="mb-6 flex flex-wrap items-center justify-end gap-3 font-montserrat">
                    {isEditing && (
                        <button
                            type="button"
                            onClick={handleReset}
                            className="cursor-pointer text-sm font-semibold uppercase text-primary-blue hover:text-nordiska-blue"
                        >
                            {t("overview-route.edit-reset")}
                        </button>
                    )}
                    <button
                        type="button"
                        onClick={toggleEditing}
                        aria-pressed={isEditing}
                        className={`cursor-pointer rounded-lg px-4 py-2 text-sm font-semibold ${isEditing
                                ? "bg-nordiska-orange text-dark-navy hover:bg-nordiska-orange-hover"
                                : "border border-primary-blue text-primary-blue hover:bg-primary-blue hover:text-white"
                            }`}
                    >
                        {isEditing ? t("overview-route.edit-done") : t("overview-route.edit-start")}
                    </button>
                </div>

                {isEditing && (
                    <p id="overview-edit-instructions" className="mb-6 text-sm text-secondary">
                        {t("overview-route.edit-instructions")}
                    </p>
                )}

                <p aria-live="polite" className="sr-only">
                    {announcement}
                </p>

                <Reorder.Group
                    values={order}
                    onReorder={setOrder}
                    className="grid grid-cols-1 gap-6 lg:auto-rows-fr lg:grid-cols-2"
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
