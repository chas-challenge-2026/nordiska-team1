import { useState, useMemo } from "react";
import { type Account } from "../../services/accountsService";
import { useTranslation } from "react-i18next";

interface AccountSelectorProps {
    accounts: Account[];
    selectedIds: number[];
    onChange: (ids: number[]) => void;
    isLoading: boolean;
    isError: boolean;
    variant?: 'panel' | 'sheet';
}

export default function AccountSelector({ accounts, selectedIds, onChange, isLoading, isError, variant = 'panel' }: AccountSelectorProps) {
    const { t } = useTranslation();
    const isSheet = variant === 'sheet';
    const allSelected = accounts.length > 0 && selectedIds.length === accounts.length;

    const [query, setQuery] = useState('');
    const [initialSelected] = useState(() => new Set(selectedIds));

    const visibleAccounts = useMemo(() => {
        if (!isSheet) return accounts;
        const q = query.trim().toLowerCase();
        const filtered = q ? accounts.filter(a => String(a.accountNumber).toLowerCase().includes(q)) : accounts;
        return [...filtered].sort((a, b) => Number(initialSelected.has(b.id)) - Number(initialSelected.has(a.id)));
    }, [accounts, query, isSheet, initialSelected]);

    const toggleAll = () => {
        onChange(allSelected ? [] : accounts.map(a => a.id));
    };

    const toggleOne = (id: number) => {
        onChange(
            selectedIds.includes(id)
                ? selectedIds.filter(x => x !== id)
                : [...selectedIds, id]
        );
    };

    return (
        <div className={isSheet ? 'bg-white px-4 pb-4 rounded-xl shadow-md border border-secondary ' : 'bg-white p-4 w-64 rounded-xl shadow-md border border-secondary'}>
            {!isSheet && (
                <>
                    <h3 className="font-semibold text-sm mb-1 border-b-1 border-nordiska-orange">{t("generic.account")}</h3>
                    <p className="text-xs text-secondary mb-3">{t("transactions-route.account-select")}</p>
                </>
            )}
            {isLoading && <p className="text-sm text-secondary">{t("transactions-route.loading-accounts")}</p>}
            {isError && <p className="text-sm text-red-700">{t("transactions-route.accounts-error")}</p>}

            {!isLoading && !isError && (
                <>
                    <div className={isSheet ? 'sticky top-0 z-10 bg-white pt-3 pb-2' : ''}>
                        {isSheet && (
                            <input
                                type="search"
                                value={query}
                                onChange={e => setQuery(e.target.value)}
                                placeholder={t("transactions-route.search-account")}
                                aria-label={t("transactions-route.search-account")}
                                className="mb-3 h-10 w-full rounded-lg border border-gray-300 bg-gray-50 px-3 text-sm"
                            />
                        )}
                        <label className="flex items-center gap-2 text-sm font-medium mb-2">
                            <input type="checkbox" checked={allSelected} onChange={toggleAll} className="cursor-pointer"/>
                            {t("transactions-route.all-accounts")}
                        </label>
                    </div>

                    <fieldset>
                        <legend className="sr-only">{t("transactions-route.account-select")}</legend>
                        <ul className={isSheet ? 'divide-y divide-gray-100' : 'space-y-2'}>
                            {visibleAccounts.map(acc => (
                                <li key={acc.id}>
                                    <label className={`flex items-start gap-2 text-sm ${isSheet ? 'py-3' : ''}`}>
                                        <input
                                            type="checkbox"
                                            checked={selectedIds.includes(acc.id)}
                                            onChange={() => toggleOne(acc.id)}
                                            className="mt-0.5 cursor-pointer"
                                        />
                                        <span className="flex flex-col">
                                            <span>{acc.accountName} | {acc.accountNumber}</span>
                                            <span className="text-secondary">
                                                {acc.balance.toLocaleString('sv-SE', { minimumFractionDigits: 2 })} sek
                                            </span>
                                        </span>
                                    </label>
                                </li>
                            ))}
                        </ul>
                        {isSheet && visibleAccounts.length === 0 && (
                            <p className="py-4 text-center text-sm text-secondary">{t("transactions-route.no-account-match")}</p>
                        )}
                    </fieldset>

                    <p className="text-xs text-secondary mt-4 mb-2">
                        {t("transactions-route.tax-info")}
                    </p>
                    <button type="button" className="w-full border cursor-pointer border-gray-300 rounded-md text-sm py-2 hover:bg-nordiska-blue bg-primary-blue text-white font-semibold">
                        {t("transactions-route.tax")}
                    </button>
                </>
            )}
        </div>
    );
}
