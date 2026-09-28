import { useGetAccounts } from "../hooks/useAccounts";
import AccountCard from "../components/AccountCard";
import { formatCurrency } from "../utils/currency";
import { useState } from "react";
import CreateAccountModal from "../components/CreateAccountModal";
import { useTranslation } from "react-i18next";

export default function AccountsPage() {
    const { data: accounts, isLoading, isError } = useGetAccounts("active");
    const [isCreateAccountOpen, setIsCreateAccountOpen] = useState(false);
    const {t} = useTranslation();

    const totalBalance = accounts?.reduce((sum, a) => sum + a.balance, 0) ?? 0;

    return (
        <div className="mx-auto  px-4 py-6 sm:px-6 sm:py-10">
            <div className=" p-4 sm:p-6">
                <div className="mb-6 flex flex-col gap-2 border-b-2 border-nordiska-orange pb-2 sm:flex-row sm:flex-wrap sm:items-baseline sm:justify-between">
                    <h1 className="text-lg font-semibold text-dark-navy">{t("accounts-route.my-accounts")}</h1>
                    {accounts && accounts.length > 0 && (
                        <p className="text-sm text-secondary">
                        {t("accounts-route.total-balance")}{" "}
                            <strong className="font-semibold text-dark-navy">
                                {formatCurrency(totalBalance)}
                            </strong>
                        </p>
                    )}
                    <button
                        type="button"
                        onClick={() => setIsCreateAccountOpen(true)}
                        className="rounded-md bg-primary-blue px-3 py-2 text-sm font-semibold text-white transition-colors hover:bg-nordiska-blue"
                    >
                    {t("accounts-route.new-account")}
                    </button>
                </div>

                {isLoading && <p className="text-sm text-secondary">{t("accounts-route.loading-accounts")}</p>}
                {isError && <p className="text-sm text-red-700">{t("accounts-route.accounts-error")}</p>}
                {accounts && accounts.length === 0 && (
                    <p className="text-sm text-secondary">{t("accounts-route.no-accounts")}</p>
                )}

                {accounts && accounts.length > 0 && (
                    <>
                        <section aria-label="Konton" className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                            {accounts.map((account) => (
                                <AccountCard key={account.id} account={account} />
                            ))}
                        </section>

                        <div className="mt-6">
                            <button
                                type="button"
                                onClick={() => { }}
                                className="w-full rounded-md bg-primary-blue py-3 text-sm font-semibold text-white transition-colors hover:bg-nordiska-blue"
                            >
                            {t("accounts-route.full-report")}
                            </button>
                        </div>
                    </>
                )}
            </div>
            <CreateAccountModal isModalOpen={isCreateAccountOpen} onClose={() => setIsCreateAccountOpen(false)} />
        </div>
    );
}
