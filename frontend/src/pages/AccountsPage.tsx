import { useTranslation } from "react-i18next";
import { useGetAccounts, useCloseAccount } from "../hooks/useAccounts";
import { useState } from "react";
import { formatCurrency } from "../utils/currency";
import Modal from "../components/modals/Modal";
import CreateAccountModal from "../components/accounts/CreateAccountModal";
import HelpCard from "../components/faq/HelpCard";
import ActionButton from "../components/accounts/ActionButton";
import HelpCardBubble from "../components/faq/HelpCardBubble";
import { useSearchParams } from "react-router";
import AccountStatementBtn from "../components/reports/AccountStatementButton";
import TaxReportBtn from "../components/reports/TaxReportButton";

/*
id: 1
customerId: 1
accountNumber: "NOR-100001"
accountType: "flex"
balance: 88210.5
interestRate: 0.035
createdAt: "2026-01-01T00:00:00Z"
accountName: "Sparkonto"
updatedAt: null
status: "active"
type: "flex"
accruedInterestYtd: 562.46
estimatedYearEndInterest: 1298.35
*/

export default function AccountsPage() {
    const {t} = useTranslation();
    const { data: accounts, isLoading, isError } = useGetAccounts("active");
    const { mutate: closeAccount, error: closeAccountError, isPending, reset } = useCloseAccount();
    const [searchParams] = useSearchParams();
    const accountIdFromUrl = Number(searchParams.get("account"));

    const [selectedAccount, setSelectedAccount] = useState<number | null>(null);
    const selectedAccountId = 
        selectedAccount ?? (accounts?.some((account) => account.id === accountIdFromUrl)
            ? accountIdFromUrl
            : accounts?.[0]?.id);

    const expandedAccount = accounts?.find((account) => account.id === selectedAccountId);
    const [isCreateAccountOpen, setIsCreateAccountOpen] = useState(false);
    const [isConfirmOpen, setIsConfirmOpen] = useState(false);
    const [isAccountNavOpen, setIsAccountNavOpen] = useState(false);

    const totalBalance = accounts?.reduce((sum, a) => sum + a.balance, 0) ?? 0;
    const canClose = expandedAccount?.balance === 0;

    const sortedAccounts = accounts
        ? [...accounts].sort((a, b) => {
            if (a.id === selectedAccountId) return -1;
            if (b.id === selectedAccountId) return 1;
            return 0;
        })
        : [];

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
                    if (nextAccount) {setSelectedAccount(nextAccount.id);} 
                    else {setSelectedAccount(null);}
                },
            });
    }

    return (
    <main className="min-h-screen w-full text-dark-navy">
        <div className="mx-auto w-full max-w-7xl px-2 sm:px-6 sm:py-8 lg:px-1 lg:py-10 ">
            {/* HEADER  */}
            <header className="mb-5">
                <div className="mb-3 grid gap-5 items-end lg:border-b-2 lg:border-nordiska-orange lg:pb-2 lg:grid-cols-[minmax(220px,280px)_1fr] xl:grid-cols-[280px_1fr_240px]">
                    {/* titel: Mina Konton */}
                    <h1 className="w-full border-b-2 border-nordiska-orange pb-2 text-2xl font-bold tracking-tight text-dark-navy sm:text-3xl lg:w-auto lg:border-0 lg:pb-0">{t("accounts-route.my-accounts")}</h1>

                    {/* saldo och knappar */}
                    <div className="flex flex-col gap-5 lg:flex-row lg:justify-between lg:items-end lg:col-span-1 xl:col-span-2">

                        {/* TOTALT SALDO */}
                        <div className="flex items-end justify-start ml-1">
                            <p className="mb-0.5 mr-3 text-sm font-medium uppercase tracking-wide text-nordiska-blue">{t("accounts-route.total-balance")}</p>
                            <p className="text-2xl font-bold tracking-tight text-dark-navy">{formatCurrency(totalBalance)}</p>
                        </div>

                        {/* ACTION BUTTONS */}
                        <div className="flex w-full flex-row gap-2 lg:w-auto">
                            <button
                                onClick={() => setIsCreateAccountOpen(true)}
                                aria-label={t("accounts-route.aria-label.new-account")}
                                className="inline-flex min-w-0 flex-1 items-center justify-center rounded-lg px-2.5 py-1.5 text-xs font-semibold text-white transition cursor-pointer bg-primary-blue hover:bg-nordiska-blue sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm"
                            >
                                <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/plus.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5" />
                                {t("accounts-route.new-account")}
                            </button>

                        <TaxReportBtn key={selectedAccountId} accountId={selectedAccountId} />
                        </div>
                    </div>
                </div>
            </header>

            {/* MAIN CONTENT */}
            <div className="grid gap-5 lg:grid-cols-[minmax(220px,280px)_1fr] xl:grid-cols-[280px_1fr_240px]">

                {/* ACCOUNT NAVIGATION */}
                <nav aria-label={t("accounts-route.my-accounts")} className="min-w-0">

                    {/* MOBILE ACCOUNT SELECTOR */}
                    <div className="lg:hidden overflow-hidden border border-primary-blue/40 bg-primary-blue/10 md:shadow-sm">

                        {/* COLLAPSIBLE HEADER */}
                        <button
                            type="button"
                            onClick={() => setIsAccountNavOpen((prev) => !prev)}
                            aria-expanded={isAccountNavOpen}
                            aria-controls="mobile-account-list"
                            className="flex w-full items-center justify-between px-3 py-2.5 text-left"
                        >
                            <div className="min-w-0">
                                <p className="truncate text-[10px] font-medium uppercase tracking-wider text-nordiska-blue"> {t("accounts-route.pick-account")} </p>

                                {expandedAccount && (
                                <>
                                    <p className="mt-0.5 truncate text-sm font-bold text-dark-navy">
                                        {expandedAccount.accountName
                                            ? expandedAccount.accountName
                                            : t(
                                                `accounts-route.account-type-${expandedAccount.accountType}`
                                            )}
                                    </p>
                                    <p className="mt-0.5 truncate text-xs text-dark-navy/70">
                                        {expandedAccount.accountNumber} ·{" "}
                                        {formatCurrency(expandedAccount.balance)}
                                    </p>
                                </>
                                )}
                            </div>

                            {/* EXPAND ICON */}
                            <span aria-hidden="true" className={`ml-3 h-4 w-4 shrink-0 border-r-2 border-b-2 border-dark-navy transition-transform duration-200 ${isAccountNavOpen ? "-rotate-[135deg]" : "-rotate-45"}`}/>
                        </button>

                        {/* MOBILE ACCOUNT LIST */}
                        {isAccountNavOpen && (
                            <div id="mobile-account-list" className="border-t border-primary-blue/40 bg-white">
                                {!isLoading && !isError && accounts?.map((account) => {
                                    const isSelected = selectedAccountId === account.id;

                                    return (
                                        <button
                                            type="button"
                                            key={account.id}
                                            onClick={() => {
                                                setSelectedAccount(account.id);
                                                setIsAccountNavOpen(false);
                                            }}
                                            className={`flex min-h-16 w-full items-center justify-between border-b border-primary-blue/30 px-3 py-2.5 text-left last:border-0 ${isSelected ? "bg-primary-blue/10" : "bg-white"}`}>
                                                
                                            <div className="min-w-0">
                                                <p className="truncate text-xs text-dark-navy">
                                                    {account.accountNumber} -{" "}
                                                    {formatCurrency(account.balance)}
                                                </p>
                                                <p className="mt-0.5 truncate text-sm font-semibold text-dark-navy">
                                                    {account.accountName ? account.accountName : t(`accounts-route.account-type-${account.accountType}`)}
                                                </p>
                                            </div>

                                            <span aria-hidden="true" className="ml-3 h-4 w-4 shrink-0 mask-[url('/icons/star.svg')] mask-contain mask-center mask-no-repeat bg-nordiska-blue"/>
                                        </button>
                                    );
                                })}

                                {isLoading && (
                                    <div className="flex min-h-16 items-center justify-center px-3" role="status" aria-live="polite">
                                        <p className="text-sm text-secondary"> {t("generic.loading")}</p>
                                    </div>
                                )}

                                {isError && (
                                    <div className="m-3 rounded-lg border border-red-200 bg-red-50 p-3" role="alert">
                                        <p className="text-sm font-medium text-red-800">{t("accounts-route.accounts-error")}</p>
                                    </div>
                                )}
                            </div>
                        )}
                    </div>

                    {/* DESKTOP ACCOUNT NAVIGATION */}
                    <div className="hidden lg:block overflow-hidden border border-gray-200 bg-white md:shadow-sm">

                        {/* NAV HEADER */}
                        <div className="flex min-h-22 items-center justify-between border-b border-primary-blue/40 bg-dark-navy px-4 py-3">
                            <div className="min-w-0 text-white">
                                <p className="truncate text-xs uppercase text-white/70">{t("accounts-route.show-accountinfo")}</p>
                                <p className="mt-1 truncate text-md font-semibold">{t("accounts-route.pick-one-account")}</p>
                            </div>
                        </div>

                        {isLoading && (
                            <div className="flex min-h-24 items-center justify-center px-4" role="status" aria-live="polite">
                                <p className="text-sm text-secondary">{t("generic.loading")}</p>
                            </div>
                        )}

                        {isError && (
                            <div className="m-4 rounded-lg border border-red-200 bg-red-50 p-3" role="alert">
                                <p className="text-sm font-medium text-red-800">{t("accounts-route.accounts-error")}</p>
                            </div>
                        )}

                        {!isLoading && !isError && accounts?.map((account) => {

                            const isSelected = selectedAccountId === account.id;

                            return (
                                <button
                                    type="button"
                                    key={account.id}
                                    onClick={() => setSelectedAccount(account.id)}
                                    className={`flex min-h-22 w-full cursor-pointer items-center justify-between border-b border-primary-blue/40 px-4 py-3 text-left last:border-0 ${isSelected ? "bg-primary-blue/10" : "bg-white hover:bg-primary-blue/10"}`}
                                >
                                    <div className="min-w-0">
                                        <p className="truncate text-sm text-dark-navy">
                                            {account.accountNumber} -{" "}
                                            {formatCurrency(account.balance)}
                                        </p>
                                        <p className="mt-1 truncate text-md font-semibold text-dark-navy">
                                            {account.accountName
                                                ? account.accountName
                                                : t(`accounts-route.account-type-${account.accountType}`)}
                                        </p>
                                    </div>
                                    <span aria-hidden="true" className="ml-3 h-5 w-5 shrink-0 mask-[url('/icons/star.svg')] mask-contain mask-center mask-no-repeat bg-nordiska-blue"/>
                                </button>
                            );
                        })}
                    </div>
                </nav>

                {/* ACCOUNT INFORMATION */}
                <section
                    aria-labelledby="account-details-heading"
                    className="min-w-0 overflow-hidden md:border md:border-gray-200 bg-white md:shadow-sm"
                >
                    {expandedAccount && (
                        <article>

                            {/* ACCOUNT HEADER */}
                            <header className="bg-dark-navy px-3 py-3 text-white sm:px-6 sm:py-5">
                                <div className="flex items-center justify-between">
                                    {/* KONTOTYP + KONTONAMN */}
                                    <div className="min-w-0">
                                        <p className="text-[10px] font-medium uppercase tracking-wider text-white/90 sm:text-xs"> {t("accounts-route.account-type")} </p>
                                        <p className="mt-0.5 truncate text-base font-bold uppercase tracking-wide sm:mt-1 sm:text-lg">
                                            {expandedAccount.accountName
                                                ? expandedAccount.accountName
                                                : t(`accounts-route.account-type-${expandedAccount.accountType}`)
                                            }
                                        </p>
                                    </div>
                                    {/* RÄNTA */}
                                    <div className="ml-4 shrink-0 text-right">
                                        <p className="text-[10px] font-medium uppercase tracking-wider text-white/90 sm:text-xs"> {t("generic.interest")} </p>
                                        <p className="mt-0.5 text-base font-bold tracking-wide sm:mt-1 sm:text-lg">
                                            {(expandedAccount.interestRate * 100).toLocaleString("sv-SE")}
                                            <span className="font-normal"> %</span>
                                        </p>
                                    </div>
                                </div>
                            </header>

                            {/* KONTONAMN & SALDO */}
                            <div className=" sm:p-4">
                                <div className="flex flex-col gap-3 border border-nordiska-blue/50 bg-primary-blue/10 p-3 sm:flex-row sm:items-end sm:justify-between sm:gap-5 sm:p-5">
                                    {/* KONTONAMN */}
                                    <div className="min-w-0">
                                        <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.account-name")}</p>
                                        <p className="mt-0.5 truncate text-xl font-bold text-dark-navy sm:mt-1 sm:text-3xl">
                                            {expandedAccount.accountName
                                                ? expandedAccount.accountName
                                                : t( `accounts-route.account-type-${expandedAccount.accountType}`)}
                                        </p>
                                    </div>

                                    {/* SALDO */}
                                    <div className="sm:text-right">
                                        <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("generic.balance")}</p>
                                        <p className="mt-0.5 text-xl font-bold tracking-tight text-dark-navy sm:mt-1 sm:text-3xl">{formatCurrency(expandedAccount.balance)}</p>
                                    </div>
                                </div>


                                {/* LINJE */}
                                <div aria-hidden="true" className="mx-auto mt-4 w-[99%] border-b-2 border-nordiska-orange sm:mt-6"/>


                                {/* INTJÄNAD & RÄNTA */}
                                <section aria-labelledby="interest-heading" className="mt-4 sm:mt-6">
                                    <h2 id="interest-heading" className="mb-2 ml-1 text-sm font-semibold text-dark-navy sm:text-base">{t("accounts-route.payback")}</h2>

                                    <div className="grid gap-2 sm:grid-cols-2 sm:gap-4">
                                        {/* UTDELNING */}
                                        <div className="border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:p-4">
                                            <p className="text-xs font-medium text-secondary sm:text-sm"> {t("accounts-route.accured-interest")} </p>
                                            <p className="mt-1 text-lg font-bold text-dark-navy sm:mt-2 sm:text-xl">{formatCurrency(expandedAccount.accruedInterestYtd)}</p>
                                            <p className="mt-1 text-[11px] leading-4 text-secondary sm:mt-2 sm:text-xs sm:leading-5">  {t("accounts-route.accured-interest-info")} </p>
                                        </div>

                                        {/* BERÄKNAD RÄNTA */}
                                        <div className="border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:p-4">
                                            <p className="text-xs font-medium text-secondary sm:text-sm">{t("accounts-route.yearly-interest")}</p>
                                            <p className="mt-1 text-lg font-bold text-dark-navy sm:mt-2 sm:text-xl">{formatCurrency(expandedAccount.estimatedYearEndInterest)}</p>
                                            <p className="mt-1 text-[11px] leading-4 text-secondary sm:mt-2 sm:text-xs sm:leading-5">{t("accounts-route.yearly-interest-info")}</p>
                                        </div>
                                    </div>
                                </section>

                                {/* LINJE */}
                                <div aria-hidden="true" className="mx-auto mt-4 w-[99%] border-b-2 border-nordiska-orange sm:mt-6"/>

                                {/* ACTIONS BUTTONS */}
                                <section aria-labelledby={t("accounts-route.aria-label.account-action-buttons")} className="my-4 sm:my-6">
                                    <div className="grid gap-2 p-1 reverse sm:grid-cols-2 sm:gap-3 xl:grid-cols-3 [direction:rtl]">

                                        {/* SPARMÅL */}
                                        <ActionButton
                                            onClick={() => {}}
                                            ariaLabel={t("accounts-route.create-savingsgoal")}
                                            prefixIcon="/icons/target.svg"
                                            title={t("accounts-route.create-savingsgoal")}
                                        />
                                        {/* KONTOUTDRAG */}
                                        <AccountStatementBtn
                                            key={expandedAccount.id}
                                            accountId={expandedAccount.id}
                                        />
                                        {/* FAVORIT */}
                                        <ActionButton
                                            onClick={() => {}}
                                            ariaLabel={t("accounts-route.favorite")}
                                            prefixIcon="/icons/star.svg"
                                            title={t("accounts-route.favorite")}
                                        />

                                        {/* AVSLUTA */}
                                        {canClose && (
                                            <ActionButton
                                                onClick={handleOpenConfirm}
                                                ariaLabel={t("accounts-route.close-account")}
                                                prefixIcon="/icons/warning.svg"
                                                prefixIconColor="bg-red-600"
                                                title={t("accounts-route.close-account")}
                                                bgColor="border border-red-600 bg-white"
                                                hoverBgColor="hover:bg-red-700"
                                                textColor="text-red-700"
                                                hoverTextColor="hover:text-white"
                                            />
                                        )}
                                    </div>
                                </section>
                            </div>
                        </article>
                    )}
                </section>

                {/* RELATERAT */}
                <aside aria-label={t("generic.help")} className="hidden xl:block min-w-0 border border-nordiska-blue/50 md:border-gray-200 bg-white md:shadow-sm">
                    <div className=" bg-nordiska-blue min-h-22 items-center justify-between px-4 py-3 text-white">
                        <p className="truncate uppercase text-sm mt-2 text-right text-white/80">{t("generic.help-center")}</p>
                        <p className="mt-1 truncate text-md text-right font-semibold">{t("generic.related-articles")}</p>
                    </div>
                    <div className="p-4">
                        <HelpCard searchTerms={t("accounts-route.help-card-search")} removeHeading numOfHits={12}/>
                    </div>
                </aside>

            </div>


            {/* CLOSE ACCOUNT MODAL */}
            <Modal
                isOpen={isConfirmOpen}
                onClose={() => setIsConfirmOpen(false)}
                title={t("accounts-route.close-confirmation")}
            >
                <div className="flex flex-col gap-4 p-5 sm:p-6">
                    <p className="font-semibold text-lg text-red-700">{t("accounts-route.close-account-info")}</p>
                    <p className="text-sm text-secondary/70 -mb-3 font-semibold uppercase">{t(`accounts-route.account-type-${expandedAccount?.accountType}`)}</p>
                    <div className="flex gap-5">
                        <p className="mt-1 text-lg font-semibold text-dark-navy">{expandedAccount?.accountName}</p>
                        <p className="mt-1 text-lg text-dark-navy">{expandedAccount?.accountNumber}</p>
                    </div>
                    {closeAccountError && (
                        <p role="alert" className="rounded-lg bg-red-50 p-3 text-sm font-medium text-red-700">
                            {closeAccountError instanceof Error ? closeAccountError.message : t("accounts-route.close-error")}
                        </p>
                    )}
                    <p className="mt-1 text-sm text-right text-red-700">{t("accounts-route.close-confirmation")}</p>

                    <div className="flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
                        <button
                            type="button"
                            onClick={() => setIsConfirmOpen(false)}
                            disabled={isPending}
                            className="min-h-11 rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-semibold text-dark-navy transition cursor-pointer hover:bg-gray-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary-blue focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {t("generic.cancel")}
                        </button>
                        <button
                            type="button"
                            onClick={handleConfirmClose}
                            disabled={isPending}
                            className="min-h-11 rounded-lg bg-red-600 px-4 py-2.5 text-sm font-semibold text-white transition cursor-pointer hover:bg-red-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-600 focus-visible:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                            {isPending ? t("accounts-route.closing"): t("accounts-route.close-account")}
                        </button>
                    </div>
                </div>
            </Modal>
            
            {/* CREATE ACCOUNT */}
            <CreateAccountModal
                isModalOpen={isCreateAccountOpen}
                onClose={() => setIsCreateAccountOpen(false)}
                onAccountCreated={(newAccount) => {
                    setSelectedAccount(newAccount.id);
                    setIsCreateAccountOpen(false);
                }}
            />

            <HelpCardBubble 
                searchTerms={t("accounts-route.help-card-search")}
                numOfHits={6}
            />
        </div>
    </main>
    );
}
