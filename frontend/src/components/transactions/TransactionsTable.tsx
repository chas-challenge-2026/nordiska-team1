import { useMemo, useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { Transaction } from '../../services/transactionsService';
import type { Account } from '../../services/accountsService';
import { formatDate, formatTime } from '../../utils/date';
import { formatCurrency } from '../../utils/currency';
import type { TransactionRowGroup } from '../../utils/transactions';
import PagePagination from '../PagePagination';

interface TransactionTableProps {
    accounts: Account[];
    /** Raderna för aktuell sida, grupperade per datum (interna överföringar redan ihopslagna) */
    groups: TransactionRowGroup[];
    shownCount: number;
    totalCount: number;
    totalPages: number;
    page: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
    isPlaceholderData: boolean;
    onPageChange: (page: number) => void;
    isLoading: boolean;
    isError: boolean;
    emptyMessage?: string;
}

function transactionTypeLabel(transaction: Transaction, t: TFunction): string {
    switch (transaction.type) {
        case "transfer": return t("transactions-route.type-transfer");
        case "deposit": return t("transactions-route.type-deposit");
        case "withdrawal": return t("transactions-route.type-withdraw");
        case "interest": return t("transactions-route.type-interest");
        case "tax": return t("transactions-route.type-tax");
        // Okända typer märks efter beloppets tecken
        default: return t(transaction.amount >= 0 ? "transactions-route.type-deposit" : "transactions-route.type-withdraw");
    }
}

export default function TransactionTable({ accounts, groups, shownCount, totalCount, totalPages, page, hasNextPage, hasPreviousPage, isPlaceholderData, onPageChange, isLoading, isError, emptyMessage }: TransactionTableProps) {
    const { t } = useTranslation();
    const listId = useId();
    const accountsById = useMemo(() => new Map(accounts.map(a => [a.id, a])), [accounts])
    const accountNumber = (id?: number) => (id ? accountsById.get(id)?.accountNumber : undefined);


    return (
        <section aria-labelledby={`${listId}-heading`} className="flex min-w-0 flex-col overflow-hidden bg-white text-dark-navy md:border md:border-gray-200 md:shadow-sm">
            {/* CARD HEADER */}
            <div className="flex min-h-22 flex-col justify-center bg-dark-navy px-4 py-3 text-white sm:px-6">
                <h2 id={`${listId}-heading`} className="truncate text-base font-bold uppercase tracking-wide sm:text-lg">{t("transactions-route.list-title")}</h2>
                <p className="mt-0.5 text-[10px] font-medium uppercase tracking-wider text-white/90 sm:mt-1 sm:text-xs" aria-live="polite">
                    {!isLoading && !isError && t("transactions-route.showing-count", { shown: shownCount, count: totalCount })}
                </p>
            </div>

            {isLoading && (
                <div className="flex min-h-40 items-center justify-center px-4" role="status" aria-live="polite">
                    <p className="text-sm text-secondary">{t("transactions-route.loading-transactions")}</p>
                </div>
            )}
            {isError && (
                <div className="m-4 rounded-lg border border-red-200 bg-red-50 p-3" role="alert">
                    <p className="text-sm font-medium text-red-800">{t("transactions-route.transactions-error")}</p>
                </div>
            )}

            {!isLoading && !isError && (
                <div
                    id={listId}
                    aria-busy={isPlaceholderData}
                    className={`flex-1 py-2 sm:p-4 ${isPlaceholderData ? 'opacity-50' : ''}`}
                >
                    {shownCount === 0 && emptyMessage && (
                        <p className="py-10 text-center text-sm text-secondary">{emptyMessage}</p>
                    )}
                    {groups.map(group => (
                        <div key={group.dateKey} className="mb-4 last:mb-0">
                            <h3 className="mb-1 border-b-2 border-nordiska-orange px-3 pb-1 text-xs font-medium text-secondary sm:px-5">
                                {formatDate(group.date)}
                            </h3>
                            <ul className="ml-3 sm:ml-5">
                                {group.rows.map(row => {
                                    let key: string | number, tx: Transaction, accountText: string | undefined, amountText: string, amountClass: string;

                                    if (row.kind === "transfer") {
                                        // Hel intern överföring: svart belopp utan tecken, som på Översikt
                                        key = row.id;
                                        tx = row.from;
                                        accountText = `${accountNumber(row.from.accountId)} → ${accountNumber(row.to.accountId)}`;
                                        amountText = formatCurrency(Math.abs(row.from.amount));
                                        amountClass = 'text-dark-navy';
                                    } else {
                                        key = row.tx.id;
                                        tx = row.tx;
                                        const own = accountNumber(tx.accountId);
                                        // Ensamt överföringsben visas som "från → till"
                                        const other = accountNumber(tx.targetAccountId);
                                        accountText = other ? (tx.amount < 0 ? `${own} → ${other}` : `${other} → ${own}`) : own;
                                        amountText = formatCurrency(tx.amount, { signed: true });
                                        amountClass = tx.amount < 0 ? 'text-red-700' : 'text-green-700';
                                    }

                                    return (
                                        <li key={key} className="px-3 py-3 odd:bg-white even:bg-light-gray/60 sm:px-5">
                                            <p className="flex items-baseline justify-between gap-2 text-sm sm:text-base">
                                                <span className="min-w-0 wrap-break-word font-semibold">{tx.label || transactionTypeLabel(tx, t)}</span>
                                                <span className={`shrink-0 whitespace-nowrap font-medium ${amountClass}`}>{amountText}</span>
                                            </p>
                                            <p className="mt-0.5 flex flex-wrap justify-between gap-x-2 gap-y-1 text-[10px] uppercase text-dark-navy/60 sm:text-xs">
                                                <span>{formatTime(tx.createdAt)}</span>
                                                {accountText && (
                                                    <span className="min-w-0 wrap-break-word text-right">{accountText}</span>
                                                )}
                                            </p>
                                        </li>
                                    );
                                })}
                            </ul>
                        </div>
                    ))}
                </div>
            )}

            <div className="border-t-2 border-nordiska-orange px-2 py-3 sm:mx-4">
                <PagePagination
                    page={page}
                    totalPages={totalPages}
                    hasPreviousPage={hasPreviousPage}
                    hasNextPage={hasNextPage}
                    onPrevious={() => onPageChange(page - 1)}
                    onNext={() => onPageChange(page + 1)}
                    onPageChange={onPageChange}
                    isDisabled={isLoading}
                />
            </div>
        </section>
    );
}
