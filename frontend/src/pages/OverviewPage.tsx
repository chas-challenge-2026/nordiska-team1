import { useState, type ComponentType } from "react";
import { Reorder } from "motion/react";
import { useTranslation } from "react-i18next";
import BalanceCard from "../components/overview/BalanceCard";
import SavingsGoalsCard from "../components/overview/SavingsGoalsCard";
import PlannedTransfersCard from "../components/overview/PlannedTransfersCard";
import RecentTransactionsCard from "../components/overview/RecentTransactionsCard";
import ReorderableCard from "../components/overview/ReorderableCard";
import InboxCard from "../components/overview/InboxCard";
import InterestCard from "../components/overview/InterestCard";
import { useCardOrder, parsePreference, serializePreference, type CardId, type OverviewPreference, } from "../components/overview/useCardOrder";
import { useUserStore } from "../store/userStore";
import { useUpdateOverviewLayout } from "../hooks/useUpdateOverviewLayout";
import OverviewHeader from "../components/overview/OverviewHeader";
import AccountCard from "../components/overview/AccountCard";
import { useGetAccounts } from "../hooks/useAccounts";
import { LoadingState, ErrorState } from "../components/StatusMessage";
import { useNavigate } from "react-router";
import CreateAccountModal from "../components/accounts/CreateAccountModal";
import { NoFavorites, NoAccounts } from "../components/StatusMessage";

export type OverviewCardProps = {showOnlyFavs: boolean;};
const CARD_COMPONENTS: Record<CardId, ComponentType<OverviewCardProps>> = {
    accounts: BalanceCard,
    savings: SavingsGoalsCard,
    planned: PlannedTransfersCard,
    transactions: RecentTransactionsCard,
    inbox: InboxCard,
    interest: InterestCard
};

// Wrappern väntar tills sessionen är klar. Först då finns `user`, så useState i OverviewContent får rätt startvärden direkt.
export default function OverviewPage() {
    const isCheckingSession = useUserStore((state) => state.isCheckingSession);
    const user = useUserStore((state) => state.user);
    if (isCheckingSession) {return null;}
    return <OverviewContent initialPref={parsePreference(user?.overviewPreference)} />;
}

function OverviewContent({ initialPref }: { initialPref: OverviewPreference }) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const user = useUserStore((state) => state.user);
    const { order, setOrder, beginEditing, move, reset } = useCardOrder();
    const updateOverviewLayout = useUpdateOverviewLayout();
    const [isEditing, setIsEditing] = useState(false);
    const [announcement, setAnnouncement] = useState("");
    const [showUnextended, setShowUnextended] = useState(initialPref.showUnextended);
    const [showOnlyFavs, setShowOnlyFavs] = useState(initialPref.showOnlyFavs);
    const [isCreateAccountOpen, setIsCreateAccountOpen] = useState(false);

    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts("active");

    // Toggles sparas med den senast SPARADE ordningen, inte en halvfärdig drag-ordning.
    function savePreference(next: Partial<Pick<OverviewPreference, "showUnextended" | "showOnlyFavs">>) {
        const savedOrder = parsePreference(user?.overviewPreference).order;
        updateOverviewLayout.mutate(serializePreference({ showUnextended, showOnlyFavs, order: savedOrder, ...next }),);
    }

    function handleSetUnextended(value: boolean) {
        if (value === showUnextended) return;
        setShowUnextended(value);
        savePreference({ showUnextended: value });
    }

    function handleToggleFavs() {
        const value = !showOnlyFavs;
        setShowOnlyFavs(value);
        savePreference({ showOnlyFavs: value });
    }

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
            await updateOverviewLayout.mutateAsync(
                serializePreference({ showUnextended, showOnlyFavs, order }),
            );
            setIsEditing(false);
            setAnnouncement(t("overview-route.edit-done"));
        } catch (error) {
            console.error("Could not save overview layout", error);
            setAnnouncement(t("overview-route.edit-save-error"));
        }
    }

    const displayedAccounts = showOnlyFavs ? accounts?.filter((account) => account.isFavorite) : accounts;

    return (
        <div className="flex flex-col items-center flex-1 w-full bg-nordiska-bg px-3 py-5 sm:px-6 sm:py-8 xl:max-w-7xl mx-auto">
            <div className={`z-10 h-7 w-full flex justify-between mb-3`}>
                <div className="flex gap-3">

                {/* ----- DESKTOP ----- */}
                    {/* visa kontoöversikt  */}
                    <button
                        type="button"
                        onClick={() => handleSetUnextended(false)}
                        className={` group hidden min-[700px]:flex items-center text-sm font-medium text-primary-blue border rounded-lg px-2 py-1  cursor-pointer
                            text-primary-blue hover:text-white hover:bg-primary-blue ${!showUnextended ? "bg-primary-blue text-white" : " text-primary-blue hover:text-white hover:bg-primary-blue"}`}
                    >
                        <span aria-hidden="true" className={`block size-4.5 ${!showUnextended ? "bg-white" : "bg-primary-blue group-hover:bg-white"} mask-[url('/icons/card-full.svg')] mask-contain mask-center mask-no-repeat`}/>
                        <span className="hidden min-[475px]:inline ml-1">{t("overview-route.view-default")}</span>
                    </button>

                    {/* visa utökad översikt */}
                    <button
                        type="button"
                        onClick={() => handleSetUnextended(true)}
                        className={` group hidden min-[700px]:flex items-center text-sm font-medium text-primary-blue border rounded-lg px-2 py-1  cursor-pointer
                            text-primary-blue hover:text-white hover:bg-primary-blue ${showUnextended ? "bg-primary-blue text-white" : " text-primary-blue hover:text-white hover:bg-primary-blue"}`}
                    >
                        <span aria-hidden="true" className={`block size-4.5 ${showUnextended ? "bg-white" : "bg-primary-blue group-hover:bg-white"} mask-[url('/icons/card-row.svg')] mask-contain mask-center mask-no-repeat`}/>
                        <span className="hidden min-[475px]:inline ml-1">{t("overview-route.view-extended")}</span>
                    </button>

                    {/* visa bara favoriter  */}
                    <div className="hidden min-[910px]:flex items-center border border-primary-blue rounded-lg px-1.5">
                        <input id="show-favorites" type="checkbox" checked={showOnlyFavs} onChange={handleToggleFavs} className="cursor-pointer "/>
                        <label htmlFor="show-favorites" className="text-sm font-medium text-primary-blue ml-1 cursor-pointer ">{t("overview-route.view-favorites")}</label>
                    </div>


                {/* ----- MOBILE ----- */}
                    {/* toggla utökad översikt */}
                    <button
                        type="button"
                        onClick={() => handleSetUnextended(!showUnextended)}
                        className={` group flex min-[700px]:hidden items-center text-sm font-medium text-primary-blue border rounded-lg px-2 py-1  cursor-pointer
                            text-primary-blue hover:text-white hover:bg-primary-blue`}
                    >
                        <span aria-hidden="true" className={`block size-4.5 mask-contain mask-center mask-no-repeat  md:mr-1.5 bg-primary-blue group-hover:bg-white`}style={{maskImage: `url('/icons/${showUnextended ? "card-full" : "card-row"}.svg')`,}}/>
                        <span className="hidden min-[475px]:inline ml-1">{showUnextended ? t("overview-route.view-extended") : t("overview-route.view-default") }</span>
                    </button>

                    {/* toggla favoriter */}
                    <button 
                        type="button" 
                        onClick={handleToggleFavs} 
                        className={` group flex min-[910px]:hidden items-center text-sm font-medium border border-primary-blue rounded-lg px-2 py-1 cursor-pointer ${showOnlyFavs ? "bg-primary-blue hover:bg-primary-hover" : "hover:bg-primary-blue"}`}
                    >
                        <span aria-hidden="true" className={`block size-4.5 mask-contain mask-center mask-no-repeat  md:mr-1.5 ${showOnlyFavs ? "bg-white" : "bg-primary-blue group-hover:bg-white"}`} style={{maskImage: `url('/icons/${showOnlyFavs ? "star-solid" : "star"}.svg')`,}}/>
                    </button>

                </div>

                {/* ----- EDIT LAYOUT ----- */}
                { showUnextended &&
                    <div className={`flex shrink-0 items-center gap-2`}>
                        {isEditing && (
                            <button
                                type="button"
                                onClick={handleReset}
                                className="group flex items-center text-sm font-medium text-primary-blue border rounded-lg px-2 py-1  cursor-pointer hover:text-white hover:bg-primary-blue"
                            >
                                <span aria-hidden="true" className="block size-4 bg-primary-blue mask-[url('/icons/reset.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-white" />
                                <span className="hidden md:inline sm:ml-1.5">{t("overview-route.edit-reset")}</span>
                            </button>
                        )}

                        <button
                            type="button"
                            onClick={isEditing ? handleDone : handleStartEditing}
                            disabled={updateOverviewLayout.isPending}
                            aria-pressed={isEditing}
                            className={`group flex items-center text-sm font-medium text-primary-blue border rounded-lg px-2 py-1  cursor-pointer
                                ${isEditing ? "bg-primary-blue text-white hover:bg-primary-hover" : "text-primary-blue hover:text-white hover:bg-primary-blue"}`}
                        >
                            <span aria-hidden="true" className={`block size-4.5 mask-contain mask-center mask-no-repeat  md:mr-1.5 ${isEditing ? "bg-white" : "bg-primary-blue group-hover:bg-white"}`} style={{maskImage: `url('/icons/${isEditing ? "done" : "grip"}.svg')`,}}/>
                            <span className="hidden md:inline"> {isEditing ? t("overview-route.edit-done") : t("overview-route.edit-start")} </span>
                            <span className={`hidden min-[475px]:inline md:hidden ${isEditing ? "" : "ml-1"}`}>{isEditing ? "" : t("overview-route.edit-start")}</span>
                        </button>
                    </div>
                }
            </div>
            <div className="mx-auto w-full">
                    <>
                        <OverviewHeader />

                        {accounts?.length === 0 && (<NoAccounts onCreateAccount={() => setIsCreateAccountOpen(true)}/> )}
                        {displayedAccounts?.length === 0 && accounts?.length !== 0 && showOnlyFavs && (<NoFavorites /> )}
                        {accountsLoading && ( <LoadingState title={t("overview-route.account-card.pending")} />)}
                        {accountsError && ( <ErrorState title={t("overview-route.account-card.error")} />)}

                        {displayedAccounts?.length !== 0 && accounts?.length !== 0 && !accountsLoading && !accountsError &&(
                            showUnextended 
                                ? (
                                    <>
                                    <p aria-live="polite" className="sr-only"> {announcement} </p>
                                    <Reorder.Group
                                        values={order}
                                        onReorder={isEditing ? setOrder : () => {}}
                                        className="grid grid-cols-1 gap-4 sm:grid-cols-2 sm:gap-5 lg:grid-cols-3 lg:gap-6 pt-3"
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
                                                    <Card showOnlyFavs={showOnlyFavs} />
                                                </ReorderableCard>
                                            );
                                        })}
                                    </Reorder.Group>
                                    </>
                                ) : (
                                    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 sm:gap-5 lg:grid-cols-3 lg:gap-8 pt-3">
                                        {displayedAccounts?.map((account) => (<AccountCard key={account.id} account={account}/>))}
                                    </div>
                                )
                        )}
                    </>
            </div>

            {/* Modalen */}
            <CreateAccountModal
                isModalOpen={isCreateAccountOpen}
                onClose={() => setIsCreateAccountOpen(false)}
                onAccountCreated={(account) => {
                    setIsCreateAccountOpen(false);
                    navigate(`/accounts?account=${account.id}`);
                }}
            />
        </div>
    );
}