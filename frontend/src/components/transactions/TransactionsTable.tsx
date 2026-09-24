import { useState, useEffect, useRef, useCallback, useMemo, useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import { type TransactionFilters } from './TransactionsFilter';
import type { Transaction } from '../../services/transactionsService';
import { formatDate, formatTime, toDateKey, toTimestamp } from '../../utils/date';
import { formatCurrency } from '../../utils/currency';

interface TransactionTableProps {
    transactions: Transaction[];
    selectedAccountIds: number[];
    filters: TransactionFilters;
    isLoading: boolean;
    isError: boolean;
}

const INITIAL_COUNT = 6;
const BATCH_SIZE = 10;

function transactionTypeLabel(type: Transaction["type"], t: TFunction): string {
    return t(type === "Deposit" ? "transactions-route.type-deposit" : "transactions-route.type-withdraw");
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

/** Filters by account, date range and type. Returns newest first. */
function filterTransactions(
    transactions: Transaction[],
    selectedAccountIds: number[],
    filters: TransactionFilters
): Transaction[] {
    return transactions
        .filter(transaction => {
            if (!selectedAccountIds.includes(transaction.accountId)) return false;
            const dateKey = toDateKey(transaction.createdAt);
            if (filters.dateFrom && dateKey < filters.dateFrom) return false;
            if (filters.dateTo && dateKey > filters.dateTo) return false;
            if (filters.onlyDeposits && transaction.amount < 0) return false;
            if (filters.onlyWithdrawals && transaction.amount >= 0) return false;
            return true;
        })
        .sort((a, b) => toTimestamp(b.createdAt) - toTimestamp(a.createdAt));
}

export default function TransactionTable({ transactions, selectedAccountIds, filters, isLoading, isError }: TransactionTableProps) {
    const allMatches = useMemo(() => filterTransactions(transactions, selectedAccountIds, filters), [transactions, selectedAccountIds, filters]);
    const [prevProps, setPrevProps] = useState({ selectedAccountIds, filters });
    const [visibleCount, setVisibleCount] = useState(INITIAL_COUNT);
    const [expanded, setExpanded] = useState(false);
    const { t } = useTranslation();
    const listId = useId();

    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const sentinelRef = useRef<HTMLDivElement>(null);

    // Reset paging when filters change (React "adjust state during render" pattern).
    if (prevProps.selectedAccountIds !== selectedAccountIds || prevProps.filters !== filters) {
        setPrevProps({ selectedAccountIds, filters });
        setVisibleCount(INITIAL_COUNT);
        setExpanded(false);
    }

    const loadMore = useCallback(() => {
        setVisibleCount(prev => Math.min(prev + BATCH_SIZE, allMatches.length));
    }, [allMatches.length]);

    const handleShowMoreClick = () => {
        setExpanded(true);
        loadMore();
    };

    const handleShowLessClick = () => {
        setExpanded(false);
        setVisibleCount(INITIAL_COUNT);
    };

    useEffect(() => {
        if (!expanded) return;
        const sentinel = sentinelRef.current;
        const root = scrollContainerRef.current;
        if (!sentinel || !root) return;

        const observer = new IntersectionObserver(
            entries => {
                if (entries[0].isIntersecting && visibleCount < allMatches.length) {
                    loadMore();
                }
            },
            { root, threshold: 0.1 }
        );

        observer.observe(sentinel);
        return () => observer.disconnect();
    }, [expanded, visibleCount, allMatches.length, loadMore]);

    const visibleItems = allMatches.slice(0, visibleCount);
    const hasMore = visibleCount < allMatches.length;
    const groupedItems = groupByDate(visibleItems);

    return (
        <div className="p-4 flex-1">
            <h3 className="font-semibold text-sm mb-1 border-b-2 border-nordiska-orange">{t("transactions-route.transactions")}</h3>

            {isLoading && <p className="text-sm text-secondary">{t("transactions-route.loading-transactions")}</p>}
            {isError && <p className="text-sm text-red-700">{t("transactions-route.transactions-error")}</p>}

            {!isLoading && !isError && (
                <>
                    <p className="text-xs text-secondary mb-3" aria-live="polite">
                        {t("transactions-route.showing-count", { shown: visibleItems.length, count: allMatches.length })}
                    </p>

                    <div
                        ref={scrollContainerRef}
                        id={listId}
                        className={expanded ? 'max-h-[85%] overflow-y-auto' : ''}
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

                        {expanded && hasMore && <div ref={sentinelRef} className="h-4" />}
                    </div>

                    {(hasMore || expanded) && (
                        <button
                            type="button"
                            onClick={expanded ? handleShowLessClick : handleShowMoreClick}
                            aria-expanded={expanded}
                            aria-controls={listId}
                            className="w-full border border-gray-300 rounded-md text-sm py-2 mt-3 hover:bg-nordiska-blue bg-primary-blue text-white font-semibold"
                        >
                            {expanded ? t("transactions-route.less-transactions") : t("transactions-route.all-transactions")}
                        </button>
                    )}
                </>
            )}
        </div>
    );
}
