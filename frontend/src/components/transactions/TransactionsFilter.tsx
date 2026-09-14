import { useState } from 'react';
import { useTranslation } from 'react-i18next';

export interface TransactionFilters {
    search: string;
    dateFrom: string;
    dateTo: string;
    onlyDeposits: boolean;
    onlyWithdrawals: boolean;
}

interface TransactionFilterProps {
    onChange: (filters: TransactionFilters) => void;
    onReset: () => void;
}

const initialFilters: TransactionFilters = {
    search: '',
    dateFrom: '',
    dateTo: '',
    onlyDeposits: false,
    onlyWithdrawals: false,
};

export default function TransactionFilter({ onChange, onReset }: TransactionFilterProps) {
    const [filters, setFilters] = useState<TransactionFilters>(initialFilters);
    const { t } = useTranslation();

    const typeFilter = filters.onlyDeposits ? 'deposits' : filters.onlyWithdrawals ? 'withdrawals' : 'all';

    const update = (patch: Partial<TransactionFilters>) => {
        const next = { ...filters, ...patch };
        setFilters(next);
        onChange(next);
    };

    function resetFilter() {
        setFilters(initialFilters);
        onReset();
    }

    return (
        <search className="bg-white p-4">
            <h3 className="font-semibold text-sm mb-3 border-b-1 border-nordiska-orange">{t("generic.filter")}</h3>

            <label htmlFor="tx-search" className="block text-xs text-secondary mb-1">{t("transactions-route.search")}</label>
            <input
                id="tx-search"
                type="text"
                value={filters.search}
                onChange={e => update({ search: e.target.value })}
                className="w-full border border-gray-300 rounded-md text-sm px-2 py-1.5 mb-4"
            />

            <div className="flex flex-col gap-2 mb-4">
                <div className="flex-1">
                    <label htmlFor="tx-date-from" className="block text-xs text-secondary mb-1">{t("transactions-route.from")}</label>
                    <input
                        id="tx-date-from"
                        type="date"
                        value={filters.dateFrom}
                        onChange={e => update({ dateFrom: e.target.value })}
                        className="w-full border border-gray-300 rounded-md text-sm px-2 py-1.5"
                    />
                </div>
                <div className="flex-1">
                    <label htmlFor="tx-date-to" className="block text-xs text-secondary mb-1">{t("transactions-route.to")}</label>
                    <input
                        id="tx-date-to"
                        type="date"
                        value={filters.dateTo}
                        onChange={e => update({ dateTo: e.target.value })}
                        className="w-full border border-gray-300 rounded-md text-sm px-2 py-1.5"
                    />
                </div>
            </div>

            <fieldset className="mb-2">
                <legend className="block text-xs text-secondary mb-1">{t("transactions-route.filter-options")}</legend>

                <label className="flex items-center gap-2 text-sm mb-2">
                    <input
                        type="radio"
                        name="tx-type-filter"
                        value="all"
                        checked={typeFilter === 'all'}
                        onChange={() => update({ onlyDeposits: false, onlyWithdrawals: false })}
                    />
                    {t("generic.all")}
                </label>

                <label className="flex items-center gap-2 text-sm mb-2">
                    <input
                        type="radio"
                        name="tx-type-filter"
                        value="deposits"
                        checked={typeFilter === 'deposits'}
                        onChange={() => update({ onlyDeposits: true, onlyWithdrawals: false })}
                    />
                    {t("transactions-route.deposites")}
                </label>

                <label className="flex items-center gap-2 text-sm">
                    <input
                        type="radio"
                        name="tx-type-filter"
                        value="withdrawals"
                        checked={typeFilter === 'withdrawals'}
                        onChange={() => update({ onlyDeposits: false, onlyWithdrawals: true })}
                    />
                    {t("transactions-route.withdraws")}
                </label>
            </fieldset>

            <button onClick={() => resetFilter()} className="w-full border border-gray-300 rounded-md text-sm py-2 hover:bg-nordiska-blue mt-4 bg-primary-blue text-white font-semibold">
                {t("generic.reset")}
            </button>
        </search>
    );
}
