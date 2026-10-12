import { useTranslation } from "react-i18next"
import { formatCurrency } from "../../utils/currency";
import { useState } from "react";
import type { Account } from "../../services/accountsService";

type AccountsNavigationProps = {
    accounts?: Account[];
    accountsLoading: boolean;
    accountsError: boolean;
    selectedAccountId: number | undefined;
    expandedAccount?: Account;
    onSelectAccount: (accountId: number) => void;
}

export default function AccountsNavigation({
    accounts,
    accountsLoading,
    accountsError,
    selectedAccountId,
    expandedAccount,
    onSelectAccount,
}: AccountsNavigationProps) {
    const {t} = useTranslation();
    const [isAccountNavOpen, setIsAccountNavOpen] = useState(false);

    return ( 
        <nav aria-label={t("accounts-route.aria-label.my-accounts")} className="min-w-0">

            {/* ----------------------- */}
            {/* MOBILE ACCOUNT SELECTOR */}
            {/* ----------------------- */}
            <div className="relative lg:hidden">
                <div className="overflow-hidden border border-primary-blue/40 bg-primary-blue/10 md:shadow-sm">

                    {/* COLLAPSIBLE HEADER */}
                    <button
                        type="button"
                        onClick={() => setIsAccountNavOpen((prev) => !prev)}
                        aria-expanded={isAccountNavOpen}
                        aria-controls="mobile-account-list"
                        className="cursor-pointer flex w-full items-center justify-between hover:bg-primary-blue/10 px-3 py-2.5 text-left"
                    >
                        <div className="min-w-0">
                            <p className="truncate text-[10px] font-medium uppercase tracking-wider text-nordiska-blue">{t("accounts-route.accounts-nav.pick-account")}</p>

                            {expandedAccount && (
                                <>
                                    <p className="mt-0.5 truncate text-sm font-bold text-dark-navy">
                                        {expandedAccount.accountName
                                            ? expandedAccount.accountName
                                            : t(`accounts-route.account-type-${expandedAccount.accountType}`)}
                                    </p>
                                    <p className="mt-0.5 truncate text-xs text-dark-navy/70">{expandedAccount.accountNumber} • <span className="font-semibold">{formatCurrency(expandedAccount.balance)}</span></p>
                                </>
                            )}
                        </div>

                        {/* EXPAND ICON */}
                        <span aria-hidden="true" className={`mr-2 h-3 w-3 shrink-0 border-r-2 border-b-2 border-dark-navy transition-transform duration-200 ${isAccountNavOpen? "-rotate-[135deg]" : "-rotate-45"}`}/>
                    </button>
                </div>

                {/* MOBILE ACCOUNT DROPDOWN */}
                {isAccountNavOpen && (
                    <div id="mobile-account-list" className="absolute left-0 right-0 top-full z-50 border border-t-0 border-primary-blue/40 bg-white shadow-lg">
                        {!accountsLoading && !accountsError &&

                            accounts?.map((account) => {
                                const isSelected = selectedAccountId === account.id;

                                return (
                                    <button
                                        type="button"
                                        key={account.id}
                                        onClick={() => {
                                            onSelectAccount(account.id);
                                            setIsAccountNavOpen(false);
                                        }}
                                        className={`flex min-h-16 w-full cursor-pointer items-center justify-between border-b border-primary-blue/30 px-3 py-2.5 text-left last:border-0 hover:bg-primary-blue-hover ${isSelected ? "bg-primary-blue/10" : "bg-white"}`}>
                                        <div className="min-w-0">
                                            <p className="truncate text-xs text-dark-navy"> {account.accountNumber} -{" "} {formatCurrency(account.balance)}</p>
                                            <p className="mt-0.5 truncate text-sm font-semibold text-dark-navy">
                                                {account.accountName ? account.accountName : t(`accounts-route.account-type-${account.accountType}`)}
                                            </p>
                                        </div>

                                        {account.isFavorite && 
                                            <span aria-hidden="true" className="ml-3 h-4 w-4 shrink-0 mask-[url('/icons/star-solid.svg')] mask-contain mask-center mask-no-repeat bg-nordiska-blue"/>
                                        }
                                        
                                    </button>
                                );
                        })}

                        {accountsLoading && (
                            <div className="flex min-h-16 items-center justify-center px-3" role="status" aria-live="polite">
                                <p className="text-sm text-secondary">{t("generic.loading")}</p>
                            </div>
                        )}

                        {accountsError && (
                            <div className="m-3 rounded-lg border border-red-200 bg-red-50 p-3" role="alert">
                                <p className="text-sm font-medium text-red-800"> {t("accounts-route.errors.accounts-error")} </p>
                            </div>
                        )}
                    </div>
                )}
            </div>

            {/* -------------------------- */}
            {/* DESKTOP ACCOUNT NAVIGATION */}
            {/* -------------------------- */}
            <div className="hidden lg:block overflow-hidden border border-gray-200 bg-white md:shadow-sm">

                {/* NAV HEADER */}
                <div className="flex min-h-22 items-center justify-between border-b border-primary-blue/40 bg-dark-navy px-4 py-3">
                    <div className="min-w-0 text-white">
                        <p className="truncate text-xs uppercase text-white/70">{t("accounts-route.accounts-nav.show-accountinfo")}</p>
                        <p className="mt-1 truncate text-md font-semibold">{t("accounts-route.accounts-nav.pick-one-account")}</p>
                    </div>
                </div>

                {accountsLoading && (
                    <div className="flex min-h-24 items-center justify-center px-4" role="status" aria-live="polite">
                        <p className="text-sm text-secondary">{t("generic.loading")}</p>
                    </div>
                )}

                {accountsError && (
                    <div className="m-4 rounded-lg border border-red-200 bg-red-50 p-3" role="alert">
                        <p className="text-sm font-medium text-red-800">{t("accounts-route.errors.accounts-error")}</p>
                    </div>
                )}

                {!accountsLoading && !accountsError && accounts?.map((account) => {

                    const isSelected = selectedAccountId === account.id;

                    return (
                        <button
                            type="button"
                            key={account.id}
                            onClick={() => onSelectAccount(account.id)}
                            className={`flex min-h-22 w-full cursor-pointer items-center justify-between border-b border-primary-blue/40 px-4 py-3 text-left last:border-0 ${isSelected ? "bg-primary-blue/10" : "bg-white hover:bg-primary-blue/10"}`}
                        >
                            <div className="min-w-0">
                                <p className="truncate text-sm text-dark-navy">
                                    {account.accountNumber}  • <span className="font-semibold">{formatCurrency(account.balance)}</span>
                                </p>
                                <p className="mt-1 truncate text-md font-semibold text-dark-navy">
                                    {account.accountName
                                        ? account.accountName
                                        : t(`accounts-route.account-type-${account.accountType}`)}
                                </p>
                            </div>
                            {account.isFavorite && 
                            <span aria-hidden="true" className="ml-3 h-5 w-5 shrink-0 mask-[url('/icons/star-solid.svg')] mask-contain mask-center mask-no-repeat bg-nordiska-blue"/>
                         
                            }
                        </button>
                    );
                })}
            </div>
        </nav>

    )
}