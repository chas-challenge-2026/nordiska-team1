import { useId } from 'react';
import { useTranslation } from 'react-i18next';
import PanelHeader from './PanelHeader';

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

const INPUT_CLASS = "w-full border border-primary-blue/40 bg-white px-2 py-1.5 text-sm text-dark-navy focus:border-primary-blue";
const LABEL_CLASS = "mb-1 block text-xs uppercase tracking-wide text-nordiska-blue";

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
        <search className={isSheet ? "relative bg-white p-4 text-dark-navy" : "relative overflow-hidden border border-gray-200 bg-white text-dark-navy shadow-sm"}>
            {!isSheet && <PanelHeader subtitle={t("transactions-route.filter-subtitle")} title={t("generic.filter")} />}

            <div className={isSheet ? "" : "p-4"}>
                {!isSheet && (
                    <div className="mb-4">
                        <label htmlFor={`${uid}-search`} className={LABEL_CLASS}>{t("transactions-route.search")}</label>
                        <input
                            id={`${uid}-search`}
                            type="search"
                            value={filters.search}
                            onChange={e => update({ search: e.target.value })}
                            className={INPUT_CLASS}
                        />
                    </div>
                )}

                <div className={`mb-4 flex gap-2 ${isSheet ? 'flex-row' : 'flex-col'}`}>
                    <div className="min-w-0 flex-1">
                        <label htmlFor={`${uid}-from`} className={LABEL_CLASS}>{t("transactions-route.from")}</label>
                        <input
                            id={`${uid}-from`}
                            type="date"
                            value={filters.dateFrom}
                            onChange={e => update({ dateFrom: e.target.value })}
                            className={INPUT_CLASS}
                        />
                    </div>
                    <div className="min-w-0 flex-1">
                        <label htmlFor={`${uid}-to`} className={LABEL_CLASS}>{t("transactions-route.to")}</label>
                        <input
                            id={`${uid}-to`}
                            type="date"
                            value={filters.dateTo}
                            onChange={e => update({ dateTo: e.target.value })}
                            className={INPUT_CLASS}
                        />
                    </div>
                </div>

                <fieldset>
                    <legend className={LABEL_CLASS}>{t("transactions-route.filter-options")}</legend>
                    <div className={isSheet ? 'flex border border-primary-blue/40 bg-primary-blue/10 p-1' : 'flex flex-col gap-2'}>
                        {typeOptions.map(opt => (
                            <label
                                key={opt.value}
                                className={isSheet
                                    ? 'flex-1 cursor-pointer py-2 text-center text-xs font-semibold text-dark-navy/70 has-checked:bg-white has-checked:text-dark-navy has-checked:shadow-sm has-focus-visible:outline-2'
                                    : 'flex cursor-pointer items-center gap-2 text-sm'}
                            >
                                <input
                                    type="radio"
                                    name={`${uid}-type`}
                                    value={opt.value}
                                    checked={filters.type === opt.value}
                                    onChange={() => update({ type: opt.value })}
                                    className={isSheet ? 'sr-only' : 'cursor-pointer accent-primary-blue'}
                                />
                                {opt.label}
                            </label>
                        ))}
                    </div>
                </fieldset>

                {!isSheet && onReset && (
                    <button
                        type="button"
                        onClick={onReset}
                        className="mt-5 w-full cursor-pointer rounded-lg bg-primary-blue py-2 text-sm font-semibold text-white transition hover:bg-nordiska-blue"
                    >
                        {t("generic.reset")}
                    </button>
                )}
            </div>
        </search>
    );
}
