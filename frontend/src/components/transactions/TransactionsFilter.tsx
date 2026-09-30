import { useId } from 'react';
import { useTranslation } from 'react-i18next';

export type TransactionTypeFilter = 'all' | 'deposit' | 'withdrawal';

export interface TransactionFilters {
    search: string;
    dateFrom: string;
    dateTo: string;
    type: TransactionTypeFilter;
}

interface TransactionFilterProps {
    filters: TransactionFilters;
    onChange: (filters: TransactionFilters) => void;
    onReset?: () => void;
    variant?: 'panel' | 'sheet';
}

export default function TransactionFilter({ filters, onChange, onReset, variant = 'panel' }: TransactionFilterProps) {
    const { t } = useTranslation();
    const uid = useId();
    const isSheet = variant === 'sheet';

    const typeOptions: { value: TransactionTypeFilter; label: string }[] = [
        { value: 'all', label: t("generic.all") },
        { value: 'deposit', label: t("transactions-route.deposites") },
        { value: 'withdrawal', label: t("transactions-route.withdraws") },
    ];

    const update = (patch: Partial<TransactionFilters>) => {
        onChange({ ...filters, ...patch });
    };

    return (
        <search className="bg-white p-4 rounded-xl shadow-md border border-secondary">
            {!isSheet && (
                <h3 className="font-semibold text-sm mb-3 border-b-1 border-nordiska-orange">{t("generic.filter")}</h3>
            )}

            {!isSheet && (
                <>
                    <label htmlFor={`${uid}-search`} className="block text-xs text-secondary mb-1">{t("transactions-route.search")}</label>
                    <input
                        id={`${uid}-search`}
                        type="text"
                        value={filters.search}
                        onChange={e => update({ search: e.target.value })}
                        className="w-full border border-gray-300 rounded-md text-sm px-2 py-1.5 mb-4"
                    />
                </>
            )}

            <div className={`flex ${isSheet ? 'flex-row' : 'flex-col'} gap-2 mb-4`}>
                <div className="flex-1 min-w-0">
                    <label htmlFor={`${uid}-from`} className="block text-xs text-secondary mb-1">{t("transactions-route.from")}</label>
                    <input
                        id={`${uid}-from`}
                        type="date"
                        value={filters.dateFrom}
                        onChange={e => update({ dateFrom: e.target.value })}
                        className="w-full border border-gray-300 rounded-md text-sm px-2 py-1.5"
                    />
                </div>
                <div className="flex-1 min-w-0">
                    <label htmlFor={`${uid}-to`} className="block text-xs text-secondary mb-1">{t("transactions-route.to")}</label>
                    <input
                        id={`${uid}-to`}
                        type="date"
                        value={filters.dateTo}
                        onChange={e => update({ dateTo: e.target.value })}
                        className="w-full border border-gray-300 rounded-md text-sm px-2 py-1.5"
                    />
                </div>
            </div>

            <fieldset className="mb-2">
                <legend className="block text-xs text-secondary mb-1">{t("transactions-route.filter-options")}</legend>
                <div className={isSheet ? 'flex rounded-lg bg-gray-100 p-1' : 'flex flex-col gap-2'}>
                    {typeOptions.map(opt => (
                        <label
                            key={opt.value}
                            className={isSheet
                                ? 'flex-1 cursor-pointer rounded-md py-2 text-center text-xs font-semibold text-secondary has-checked:bg-white has-checked:text-black has-checked:shadow-sm has-focus-visible:outline-2'
                                : 'flex items-center gap-2 text-sm'}
                        >
                            <input
                                type="radio"
                                name={`${uid}-type`}
                                value={opt.value}
                                checked={filters.type === opt.value}
                                onChange={() => update({ type: opt.value })}
                                className={isSheet ? 'sr-only' : "cursor-pointer"}
                            />
                            {opt.label}
                        </label>
                    ))}
                </div>
            </fieldset>

            {!isSheet && onReset && (
                <button type="button" onClick={onReset} className="w-full border cursor-pointer border-gray-300 rounded-md text-sm py-2 hover:bg-nordiska-blue mt-4 bg-primary-blue text-white font-semibold">
                    {t("generic.reset")}
                </button>
            )}
        </search>
    );
}
