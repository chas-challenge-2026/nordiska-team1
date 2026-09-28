import { useMemo, useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { Transaction } from '../../services/transactionsService';
import { formatDate, formatTime, toDateKey } from '../../utils/date';
import { formatCurrency } from '../../utils/currency';

interface TransactionTableProps {
    transactions: Transaction[];
    totalCount: number;
    totalPages: number;
    page: number;
    hasNextPage: boolean;
    isPlaceholderData: boolean;
    onPageChange: (page: number) => void;
    isLoading: boolean;
    isError: boolean;
}

function transactionTypeLabel(type: Transaction["type"], t: TFunction): string {
    return t(type === "deposit" ? "transactions-route.type-deposit" : "transactions-route.type-withdraw");
}

/** Groups consecutive transactions by local date. Input must be sorted by date. */
function groupByDate(transactions: Transaction[]): { dateKey: string; items: Transaction[] }[] {
    const groups: { dateKey: string; items: Transaction[] }[] = [];
    for (const transaction of transactions) {
        const dateKey = toDateKey(transaction.createdAt);
        const last = groups[groups.length - 1];
        if (last && last.dateKey === dateKey) {
            last.items.push(transaction);
        } else {
            groups.push({ dateKey, items: [transaction] });
        }
    }
    return groups;
}

export default function TransactionTable({ transactions, totalCount, totalPages, page, hasNextPage, isPlaceholderData, onPageChange, isLoading, isError }: TransactionTableProps) {
    const { t } = useTranslation();
    const listId = useId();
    const groupedItems = useMemo(() => groupByDate(transactions), [transactions]);

    return (
        <div className="p-4 flex-1 min-h-0 flex flex-col">
            <h3 className="shrink-0 font-semibold text-sm mb-1 border-b-2 border-nordiska-orange">{t("transactions-route.transactions")}</h3>

            {isLoading && <p className="text-sm text-secondary">{t("transactions-route.loading-transactions")}</p>}
            {isError && <p className="text-sm text-red-700">{t("transactions-route.transactions-error")}</p>}

            {!isLoading && !isError && (
                <>
                    <p className="shrink-0 text-xs text-secondary mb-3" aria-live="polite">
                        {t("transactions-route.showing-count", { shown: transactions.length, count: totalCount })}
                    </p>

                    <div
                        id={listId}
                        aria-busy={isPlaceholderData}
                        className={`flex-1 min-h-0 overflow-y-auto ${isPlaceholderData ? 'opacity-50' : ''}`}
                    >
                        {groupedItems.map(group => (
                            <div key={group.dateKey} className="mb-3">
                                <h4 className="text-xs font-medium text-secondary mb-1 border-b-2 border-gray-200">
                                    {formatDate(group.items[0].createdAt)}
                                </h4>
                                <ul className="divide-y divide-gray-100 px-3">
                                    {group.items.map(transaction => (
                                        <li key={transaction.id} className="py-3 flex justify-between items-start">
                                            <div>
                                                <p className="text-sm font-medium">{transaction.label || transactionTypeLabel(transaction.type, t)}</p>
                                                {transaction.label && (
                                                    <p className="text-xs text-secondary">{transactionTypeLabel(transaction.type, t)}</p>
                                                )}
                                            </div>
                                            <div className="text-right">
                                                <p className="text-xs text-secondary">{formatTime(transaction.createdAt)}</p>
                                                <p className={`text-sm font-medium ${transaction.amount < 0 ? 'text-red-700' : 'text-green-700'}`}>
                                                    {formatCurrency(transaction.amount, { signed: true })}
                                                </p>
                                            </div>
                                        </li>
                                    ))}
                                </ul>
                            </div>
                        ))}
                    </div>
                </>
            )}

            <nav aria-label={t("generic.pagination")} className="shrink-0 mt-auto pt-3 flex items-center justify-between">
                <button
                    type="button"
                    onClick={() => onPageChange(page - 1)}
                    disabled={isLoading || page === 1}
                    className="border border-gray-300 rounded-md text-sm px-4 py-2 bg-primary-blue text-white font-semibold hover:bg-nordiska-blue disabled:opacity-50 disabled:cursor-not-allowed"
                >
                    {t("generic.previous")}
                </button>

                <span className="text-xs text-secondary" aria-live="polite">
                    {t("generic.page")} {page} / {totalPages}
                </span>

                <button
                    type="button"
                    onClick={() => onPageChange(page + 1)}
                    disabled={isLoading || isPlaceholderData || !hasNextPage}
                    className="border border-gray-300 rounded-md text-sm px-4 py-2 bg-primary-blue text-white font-semibold hover:bg-nordiska-blue disabled:opacity-50 disabled:cursor-not-allowed"
                >
                    {t("generic.next")}
                </button>
            </nav>
        </div>
    );
}
