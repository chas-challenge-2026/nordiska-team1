import { useState, useMemo } from "react";
import { type Account } from "../../services/accountsService";
import { useTranslation } from "react-i18next";
import { formatCurrency } from "../../utils/currency";
import PanelHeader from "./PanelHeader";

interface AccountSelectorProps {
    accounts: Account[];
    selectedIds: number[];
    onChange: (ids: number[]) => void;
    isLoading: boolean;
    isError: boolean;
    showClosed: boolean;
    onShowClosedChange: (show: boolean) => void;
    variant?: 'panel' | 'sheet';
}

export default function AccountSelector({ accounts, selectedIds, onChange, isLoading, isError, showClosed, onShowClosedChange, variant = 'panel' }: AccountSelectorProps) {
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

    const activeAccounts = visibleAccounts.filter(a => a.status !== "closed");
    const closedAccounts = visibleAccounts.filter(a => a.status === "closed");

    const renderAccount = (acc: Account) => {
        const isSelected = selectedIds.includes(acc.id);
        return (
            <li key={acc.id}>
                <label className={`flex cursor-pointer items-center gap-3 border-b border-primary-blue/40 py-3 ${isSheet ? '' : 'px-4'} ${isSelected ? 'bg-primary-blue/10' : 'bg-white hover:bg-primary-blue/10'}`}>
                    <input
                        type="checkbox"
                        checked={isSelected}
                        onChange={() => toggleOne(acc.id)}
                        className="cursor-pointer accent-primary-blue"
                    />
                    <span className="min-w-0">
                        <span className="block truncate text-xs text-dark-navy">
                            {acc.accountNumber} • <span className="font-semibold">{formatCurrency(acc.balance)}</span>
                        </span>
                        <span className="mt-0.5 block truncate text-sm font-semibold">
                            {acc.accountName || t(`accounts-route.account-type-${acc.accountType}`)}
                            {acc.status === "closed" && (
                                <span className="ml-1 text-xs font-normal text-secondary">({t("transactions-route.closed")})</span>
                            )}
                        </span>
                    </span>
                </label>
            </li>
        );
    };

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
        <div className={isSheet ? 'relative bg-white px-4 pb-4 text-dark-navy' : 'relative overflow-hidden border border-gray-200 bg-white text-dark-navy shadow-sm'}>
            {!isSheet && <PanelHeader subtitle={t("transactions-route.accounts-subtitle")} title={t("generic.account")} />}

            {isLoading && (
                <div className="flex min-h-24 items-center justify-center px-4" role="status" aria-live="polite">
                    <p className="text-sm text-secondary">{t("transactions-route.loading-accounts")}</p>
                </div>
            )}
            {isError && (
                <div className="m-4 rounded-lg border border-red-200 bg-red-50 p-3" role="alert">
                    <p className="text-sm font-medium text-red-800">{t("transactions-route.accounts-error")}</p>
                </div>
            )}

            {!isLoading && !isError && (
                <>
                    <div className={isSheet ? 'sticky top-0 z-10 bg-white pt-3' : ''}>
                        {isSheet && (
                            <input
                                type="search"
                                value={query}
                                onChange={e => setQuery(e.target.value)}
                                placeholder={t("transactions-route.search-account")}
                                aria-label={t("transactions-route.search-account")}
                                className="mb-3 h-10 w-full border border-primary-blue/40 bg-white px-3 text-sm"
                            />
                        )}
                        <label className={`flex cursor-pointer items-center gap-3 border-b border-primary-blue/40 py-3 text-sm font-semibold ${isSheet ? '' : 'px-4'}`}>
                            <input type="checkbox" checked={allSelected} onChange={toggleAll} className="cursor-pointer accent-primary-blue" />
                            {t("transactions-route.all-accounts")}
                        </label>
                    </div>

                    <fieldset>
                        <legend className="sr-only">{t("transactions-route.account-select")}</legend>
                        <ul>{activeAccounts.map(renderAccount)}</ul>
                        {isSheet && visibleAccounts.length === 0 && (
                            <p className="py-4 text-center text-sm text-secondary">{t("transactions-route.no-account-match")}</p>
                        )}

                        <label className={`flex cursor-pointer items-center gap-3 py-3 text-sm ${isSheet ? '' : 'px-4'} ${showClosed && closedAccounts.length > 0 ? 'border-b border-primary-blue/40' : ''}`}>
                            <input
                                type="checkbox"
                                checked={showClosed}
                                onChange={e => onShowClosedChange(e.target.checked)}
                                className="cursor-pointer accent-primary-blue"
                            />
                            {t("transactions-route.show-closed")}
                        </label>

                        {/* Stängda konton listas under sin egen ruta så att det syns var de kommer ifrån */}
                        {showClosed && closedAccounts.length > 0 && (
                            <ul>{closedAccounts.map(renderAccount)}</ul>
                        )}
                    </fieldset>
                </>
            )}
        </div>
    );
}
