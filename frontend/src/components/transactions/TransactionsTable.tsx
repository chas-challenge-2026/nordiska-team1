import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { transactions, type Transaction } from './mockTransactions';
import { type TransactionFilters } from './TransactionsFilter';
import { useTranslation } from 'react-i18next';

interface TransactionTableProps {
    selectedAccountIds: string[];
    filters: TransactionFilters;
}

const INITIAL_COUNT = 6;
const BATCH_SIZE = 10;

function groupByDate(transactions: Transaction[]): { dateKey: string; items: Transaction[] }[] {
    const groups: { dateKey: string; items: Transaction[] }[] = [];
    for (const transaction of transactions) {
        const last = groups[groups.length - 1];
        if (last && last.dateKey === transaction.date) {
            last.items.push(transaction);
        } else {
            groups.push({ dateKey: transaction.date, items: [transaction] });
        }
    }
    return groups;
}

function filterTransactions(
    selectedAccountIds: string[],
    filters: TransactionFilters
): Transaction[] {
    return transactions.filter(transaction => {
        if (!selectedAccountIds.includes(transaction.account)) return false;
        if (filters.search && !transaction.title.toLowerCase().includes(filters.search.toLowerCase())) return false;
        if (filters.dateFrom && transaction.date < filters.dateFrom) return false;
        if (filters.dateTo && transaction.date > filters.dateTo) return false;
        if (filters.onlyDeposits && transaction.amount < 0) return false;
        if (filters.onlyWithdrawals && transaction.amount >= 0) return false;
        return true;
    });
}

export default function TransactionTable({ selectedAccountIds, filters }: TransactionTableProps) {
    const [allMatches, setAllMatches] = useState<Transaction[]>([]);
    const [visibleCount, setVisibleCount] = useState(INITIAL_COUNT);
    const [expanded, setExpanded] = useState(false);
    const { t } = useTranslation();

    const scrollContainerRef = useRef<HTMLDivElement>(null);
    const sentinelRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
        const matches = filterTransactions(selectedAccountIds, filters);
        setAllMatches(matches);
        setVisibleCount(INITIAL_COUNT);
        setExpanded(false);
    }, [selectedAccountIds, filters]);

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

    const groupedItems = useMemo(() => groupByDate(visibleItems), [visibleItems]);

    return (
        <div className="p-4 flex-1 ">
            <h3 className="font-semibold text-sm mb-1 border-b-2 border-nordiska-orange">{t("transactions-route.transactions")}</h3>
            <p className="text-xs text-secondary mb-3">
            {t("generic.showing")} {visibleItems.length} {t("generic.of")} {allMatches.length} {t("generic.transactions")}
            </p>

            <div
                ref={scrollContainerRef}
                className={expanded ? 'max-h-[85%] overflow-y-auto' : ''}
            >
                {groupedItems.map(group => (
                    <div key={group.dateKey} className="mb-3">
                        <p className="text-xs font-medium text-secondary mb-1 border-b-2 border-gray-200">{group.dateKey}</p>
                        <ul className="divide-y divide-gray-100  px-3">
                            {group.items.map(transaction => (
                                <li key={transaction.id} className="py-3 flex justify-between items-start">
                                    <div>
                                        <p className="text-sm font-medium">{transaction.title}</p>
                                        <p className="text-xs text-secondary">{transaction.accountLabel}</p>
                                    </div>
                                    <div className="text-right">
                                        <p className="text-xs text-secondary">kl {transaction.time}</p>
                                        <p className={`text-sm font-medium ${transaction.amount < 0 ? 'text-red-700' : 'text-green-700'}`}>
                                            {transaction.amount > 0 ? '+' : ''}
                                            {transaction.amount.toLocaleString('sv-SE', { minimumFractionDigits: 2 })} sek
                                        </p>
                                    </div>
                                </li>
                            ))}
                        </ul>
                    </div>
                ))}

                {expanded && hasMore && <div ref={sentinelRef} className="h-4" />}
            </div>

            {!expanded && hasMore && (
                <button
                    onClick={handleShowMoreClick}
                    className="w-full border border-gray-300 rounded-md text-sm py-2 mt-3 hover:bg-nordiska-blue bg-primary-blue text-white font-semibold"
                >
                    {t("transactions-route.all-transactions")}
                </button>
            )}

            {expanded && (
                <button
                    onClick={handleShowLessClick}
                    className="w-full border border-gray-300 rounded-md text-sm py-2 mt-3 hover:bg-nordiska-blue bg-primary-blue text-white font-semibold"
                >
                    {t("transactions-route.less-transactions")}
                </button>
            )}
        </div>
    );
}
