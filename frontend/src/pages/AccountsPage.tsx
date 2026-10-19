import { useState, useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import axios from "axios";
import { useGetAccounts, useCloseAccount, useGetAllSavingsGoals, useUpdateFavoriteAccount } from "../hooks/useAccounts";
import { formatCurrency } from "../utils/currency";
import CreateAccountModal from "../components/accounts/CreateAccountModal";
import ActionButton from "../components/accounts/ActionButton";
import HandleSavingsGoalModal from "../components/accounts/HandleSavingGoalModal";
import AccountsNavigation from "../components/accounts/AccountNavigation";
import InterestSection from "../components/accounts/InterestSection";
import CloseAccountModal from "../components/accounts/CloseAccountModal";
import SavingsGoalSection from "../components/accounts/SavingsGoalSection";
import AccountRecentTransactions from "../components/accounts/AccountRecentTransactions";
import HelpCard from "../components/faq/HelpCard";
import HelpCardBubble from "../components/faq/HelpCardBubble";
import { useSearchParams } from "react-router";
import AccountStatementBtn from "../components/reports/AccountStatementButton";
import { Reorder } from "motion/react";
import DraggableSection from "../components/accounts/DraggableSection";
import TotalBalanceAndBtns from "../components/accounts/TotalBalanceAndBtns";

export type SectionId = "transactions" | "savings" | "interest";

export default function AccountsPage() {
    const { t } = useTranslation();
    const navigate = useNavigate();

    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts("active");
    const { data: goals } = useGetAllSavingsGoals();
    const updateFavorite = useUpdateFavoriteAccount();
    const { mutate: closeAccount, error: closeAccountError, isPending, reset } = useCloseAccount();
    const [searchParams] = useSearchParams();
    const accountIdFromUrl = Number(searchParams.get("account"));
    const [selectedAccount, setSelectedAccount] = useState<number | null>(null);

    const [isCreateAccountOpen, setIsCreateAccountOpen] = useState(false);
    const [isConfirmOpen, setIsConfirmOpen] = useState(false);
    const [isSavingGoalOpen, setIsSavingGoalOpen] = useState(false);
    const [newGoal, setNewGoal] = useState(false);
    const [favoriteError, setFavoriteError] = useState("");
    const favoriteErrorTimeout = useRef<ReturnType<typeof setTimeout> | null>(null);

    useEffect(() => {
        return () => {
            if (favoriteErrorTimeout.current) {clearTimeout(favoriteErrorTimeout.current);}
        };
    }, []);

    const sortedAccounts = accounts
        ? [...accounts].filter((account) => account.status === "active").sort((a, b) => {
            if (a.isFavorite && !b.isFavorite) return -1;
            if (!a.isFavorite && b.isFavorite) return 1;
            return 0;
        })
        : [];

    const selectedAccountId = 
        selectedAccount ?? (sortedAccounts?.some((account) => account.id === accountIdFromUrl)
            ? accountIdFromUrl
            : sortedAccounts?.[0]?.id);

    const expandedAccount = accounts?.find((account) => account.id === selectedAccountId);
    const isFavorite = expandedAccount?.isFavorite ?? false;
    const totalBalance = accounts?.reduce((sum, account) => sum + account.balance, 0) ?? 0;
    const canClose = expandedAccount?.balance === 0;
    const existingSavingGoal = goals?.goals.find((goal) => goal.accountId === selectedAccountId && goal.status === "active");

    const DEFAULT_ORDER: SectionId[] = ["transactions","savings","interest",];

    const sections = {
        transactions: (
            <>{expandedAccount &&
            <AccountRecentTransactions accountId={expandedAccount?.id}/>
            }</>
        ),
        interest: (
            <>{expandedAccount &&
                <InterestSection accruedInterestYtd={expandedAccount?.accruedInterestYtd} estimatedYearEndInterest={expandedAccount?.estimatedYearEndInterest} />
            }</>

        ),
        savings: (
            <>{expandedAccount &&
                <SavingsGoalSection
                    savingGoal={existingSavingGoal}
                    accountAmount={expandedAccount?.balance}
                    onDeposit={() => {
                        navigate("/transfer")
                    }}
                    onEdit={(newGoal) => {
                        setIsSavingGoalOpen(true);
                        setNewGoal(newGoal);
                    }}
                />
            }</>
        )
    }

    const [sectionOrders, setSectionOrders] = useState<Record<number, SectionId[]>>(() => {
        try {
            return JSON.parse(localStorage.getItem("account-section-orders") ?? "{}");
        } catch {
            return {};
        }
    });

    const accountId = expandedAccount?.id;
    const sectionOrder = accountId ? sectionOrders[accountId] ?? DEFAULT_ORDER : DEFAULT_ORDER;

    function handleReorder(newOrder: SectionId[]) {
        if (!accountId) return;
        const updatedOrders = {...sectionOrders,[accountId]: newOrder,};
        setSectionOrders(updatedOrders);
        localStorage.setItem("account-section-orders",JSON.stringify(updatedOrders));
    }

    function handleOpenConfirm() {
        reset();
        setIsConfirmOpen(true);
    }

    function handleConfirmClose() {
        if (typeof expandedAccount?.id !== "number") return;

        const currentIndex = sortedAccounts.findIndex((account) => account.id === expandedAccount.id);
        const nextAccount = sortedAccounts[currentIndex + 1] ?? sortedAccounts[currentIndex - 1];

        closeAccount(expandedAccount.id, {
            onSuccess: () => {
                setIsConfirmOpen(false);
                setSelectedAccount(nextAccount?.id ?? null);
            },
        });
    }

    function handleFavorite(accountId: number, isFavorite: boolean) {
        setFavoriteError("");

        updateFavorite.mutate(
            { id: accountId, isFavorite },
            {
                onError: (error) => {
                    if (favoriteErrorTimeout.current) {
                        clearTimeout(favoriteErrorTimeout.current);
                    }

                    if (axios.isAxiosError(error) && error.response?.status === 400) {
                        setFavoriteError(t("accounts-route.favorite-error"));
                    } else {
                        setFavoriteError(t("generic.errors.generic"));
                    }

                    favoriteErrorTimeout.current = setTimeout(() => {
                        setFavoriteError("");
                        favoriteErrorTimeout.current = null;
                    }, 3000);
                },
            },
        );
    }

    return (
        <main className="min-h-screen w-full text-dark-navy mt-3 sm:mt-4 md:mt-10">
            <div className="mx-auto w-full max-w-7xl px-2 ">

                {/* VISAS PÅ SMÅ SKÄRM */}
                       <div className="min-[1200px]:hidden">
                            <TotalBalanceAndBtns
                                selectedAccountId={selectedAccountId}
                                totalBalance={totalBalance}
                                onCreateAccount={() => setIsCreateAccountOpen(true)}
                            />
                        </div>

                <div className="grid grid-cols-1 gap-5 min-[991px]:grid-cols-[minmax(220px,280px)_minmax(0,1fr)] min-[1200px]:grid-cols-[280px_minmax(0,1fr)_240px]">

                    {/* ------------------------------- */}
                    {/* --------- COL VÄNSTER --------- */}
                    {/* ------------------------------- */}
                    <div className="flex min-w-0 flex-col gap-5">
                        {/* ACCOUNT NAVIGATION */}
                        <AccountsNavigation accounts={sortedAccounts} accountsLoading={accountsLoading} accountsError={accountsError} selectedAccountId={selectedAccountId} expandedAccount={expandedAccount} onSelectAccount={setSelectedAccount} />
                    </div>

                    {/* ------------------------------ */}
                    {/* --------- COL MITTEN --------- */}
                    {/* ------------------------------ */}
                    {/* ACCOUNT INFORMATION */}
                    <section aria-labelledby="account-details-heading" className="min-w-0 overflow-hidden min-[991px]:bg-white min-[991px]:border min-[991px]:border-gray-200 min-[991px]:shadow-sm">
                        {expandedAccount && (
                            <article>
                                {/* CARD HEADER */}
                                <div className="bg-dark-navy px-3 py-3 text-white sm:px-6 sm:py-5">
                                    <div className="flex items-center justify-between">
                                        {/* ACCOUNT TYPE */}
                                        <div className="min-w-0">
                                            <p className="text-[10px] font-medium uppercase tracking-wider text-white/90 sm:text-xs">{t("accounts-route.account-type")}</p>
                                            <p className="mt-0.5 truncate text-base font-bold tracking-wide sm:mt-1 sm:text-lg">{t(`accounts-route.account-type-${expandedAccount.accountType}`)}</p>
                                        </div>

                                        {/* INTEREST RATE */}
                                        <div className="ml-4 shrink-0 text-right">
                                            <p className="text-[10px] font-medium uppercase tracking-wider text-white/90 sm:text-xs">{t("generic.interest")}</p>
                                            <p className="mt-0.5 text-base font-bold tracking-wide sm:mt-1 sm:text-lg">
                                                {(expandedAccount.interestRate * 100).toLocaleString("sv-SE")}<span className="font-normal"> %</span>
                                            </p>
                                        </div>
                                    </div>
                                </div>

                                <div className="sm:p-4">
                                    {/* ACCOUNT NAME AND BALANCE */}
                                    <div className="flex flex-col gap-3 border border-nordiska-blue/50 bg-primary-blue/10 p-3 sm:flex-row sm:items-end sm:justify-between sm:gap-5 sm:p-5">
                                        <div className="min-w-0">
                                            <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{expandedAccount.accountNumber}</p>
                                            <p className="mt-0.5 truncate text-xl font-bold text-dark-navy sm:mt-1 sm:text-3xl">
                                                {expandedAccount.accountName || t(`accounts-route.account-type-${expandedAccount.accountType}`)}
                                            </p>
                                        </div>

                                        <div className="sm:text-right">
                                            <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("generic.balance")}</p>
                                            <p className="mt-0.5 text-xl font-bold tracking-tight text-dark-navy sm:mt-1 sm:text-3xl">{formatCurrency(expandedAccount.balance)}</p>
                                        </div>
                                    </div>

                                    {/* SAVING, TRANSACTIONS, INTEREST SECTIONS */}
                                    <Reorder.Group
                                        axis="y"
                                        values={sectionOrder}
                                        onReorder={handleReorder}
                                        className="flex flex-col"
                                    >
                                        {sectionOrder.map((id) => (
                                            <DraggableSection key={id} id={id}>
                                                {sections[id]}
                                            </DraggableSection>
                                        ))}
                                    </Reorder.Group>

                                    {/* DIVIDER */}
                                    <div aria-hidden="true" className="mx-auto mt-4 w-[99%] border-b-2 border-nordiska-orange sm:mt-8" />

                                    {/* OTHER ACTIONS */}
                                    <h2 id="account-actions-heading" className="mt-4 mb-1 ml-1 text-base font-semibold text-dark-navy sm:mt-6 sm:text-xl">{t("accounts-route.actions")}</h2>

                                    <section aria-labelledby="account-actions-heading" className="my-4 sm:my-6">
                                        <div className="grid gap-2 p-1 sm:grid-cols-2 sm:gap-3 xl:grid-cols-3">
                                            {favoriteError ? (
                                                <div className="col-span-full rounded-lg border border-red-200 bg-red-50 py-3 text-center" role="alert">
                                                    <p className="text-sm font-medium text-red-800">{favoriteError}</p>
                                                </div>
                                            ) : (
                                                <>
                                                    {/* ACCOUNT STATEMENT */}
                                                    <AccountStatementBtn key={expandedAccount.id} accountId={expandedAccount.id} />

                                                    {/* FAVORITE */}
                                                    <ActionButton
                                                        onClick={() => handleFavorite(expandedAccount.id, !expandedAccount.isFavorite)}
                                                        disabled={updateFavorite.isPending}
                                                        ariaLabel={isFavorite ? t("accounts-route.no-favorite") : t("accounts-route.favorite")}
                                                        prefixIcon={isFavorite ? "/icons/star-solid.svg" : "/icons/star.svg"}
                                                        title={isFavorite ? t("accounts-route.no-favorite") : t("accounts-route.favorite")}
                                                    />

                                                    {/* CLOSE ACCOUNT */}
                                                    {canClose && (
                                                        <ActionButton
                                                            style="cancel"
                                                            onClick={handleOpenConfirm}
                                                            ariaLabel={t("accounts-route.close-account")}
                                                            prefixIcon="/icons/warning.svg"
                                                            title={t("accounts-route.close-account")}
                                                        />
                                                    )}
                                                </>
                                            )}
                                        </div>
                                    </section>
                                </div>
                            </article>
                        )}
                    </section>

                    {/* ----------------------------- */}
                    {/* --------- COL HÖGER --------- */}
                    {/* ----------------------------- */}
                    <div className="hidden min-[1200px]:flex min-[1200px]:flex-col min-[1200px]:gap-5">
                       
                        <TotalBalanceAndBtns selectedAccountId={selectedAccountId} totalBalance={totalBalance} onCreateAccount={() => setIsCreateAccountOpen(true)} />

                        {/* HELP / RELATED ARTICLES */}
                        <aside aria-label={t("generic.help")} className="hidden min-w-0 self-start border border-gray-200 bg-white md:shadow-sm md:block">
                            <div className="flex min-h-22 items-center justify-between border-b border-primary-blue/40 bg-nordiska-blue px-4 py-3">
                                <div className="min-w-0 text-white">
                                    <p className="truncate text-xs uppercase text-white/70">{t("generic.help-center")}</p>
                                    <p className="mt-1 truncate text-md font-semibold">{t("generic.related-articles")}</p>
                                </div>
                            </div>

                            <div className="p-4">
                                <HelpCard searchTerms={t("accounts-route.help-card-search")} removeHeading numOfHits={12} />
                            </div>
                        </aside>
                    </div>
                </div>

                {/* --------------------------- */}
                {/* --------- MODALER --------- */}
                {/* --------------------------- */}

                {/* CLOSE ACCOUNT MODAL */}
                <CloseAccountModal
                    isConfirmOpen={isConfirmOpen}
                    expandedAccount={expandedAccount}
                    closeAccountError={closeAccountError}
                    isPending={isPending}
                    onClose={() => setIsConfirmOpen(false)}
                    onConfirm={handleConfirmClose}
                />

                {/* SAVINGS GOAL MODAL */}
                {isSavingGoalOpen && expandedAccount && (
                    <HandleSavingsGoalModal
                        accountId={expandedAccount.id}
                        accountNumber={expandedAccount.accountNumber}
                        accountName={expandedAccount.accountName}
                        isModalOpen={isSavingGoalOpen}
                        onClose={() => setIsSavingGoalOpen(false)}
                        existingSavingGoal={existingSavingGoal}
                        newGoal={newGoal}
                    />
                )}

                {/* CREATE ACCOUNT MODAL */}
                <CreateAccountModal
                    isModalOpen={isCreateAccountOpen}
                    onClose={() => setIsCreateAccountOpen(false)}
                    onAccountCreated={(newAccount) => {
                        setSelectedAccount(newAccount.id);
                        setIsCreateAccountOpen(false);
                    }}
                />

                {/* HELP BUBBLE */}
                <div className="min-[1200px]:hidden">
                    <HelpCardBubble searchTerms={t("accounts-route.help-card-search")} numOfHits={6} />
                </div>  
            </div>
        </main>
    );
}
