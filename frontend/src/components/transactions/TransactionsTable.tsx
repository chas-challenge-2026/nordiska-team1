import { useState, useEffect, useRef, useCallback, useMemo } from 'react';
import { transactions, accounts, type Transaction } from './mockTransactions';
import { type TransactionFilters } from './TransactionsFilter';
import { useTranslation } from 'react-i18next';

interface TransactionTableProps {
    selectedAccountIds: string[];
    filters: TransactionFilters;
}

const INITIAL_COUNT = 6;
const BATCH_SIZE = 10;

function groupByDate(items: Transaction[]): { dateKey: string; items: Transaction[] }[] {
    const groups: { dateKey: string; items: Transaction[] }[] = [];
    for (const tx of items) {
        const last = groups[groups.length - 1];
        if (last && last.dateKey === tx.date) {
            last.items.push(tx);
        } else {
            groups.push({ dateKey: tx.date, items: [tx] });
        }
    }
    return groups;
}

function filterTransactions(
    selectedAccountIds: string[],
    filters: TransactionFilters
): Transaction[] {
    return transactions.filter(tx => {
        if (!selectedAccountIds.includes(tx.account)) return false;
        if (filters.search && !tx.title.toLowerCase().includes(filters.search.toLowerCase())) return false;
        if (filters.dateFrom && tx.date < filters.dateFrom) return false;
        if (filters.dateTo && tx.date > filters.dateTo) return false;
        if (filters.onlyDeposits && tx.amount < 0) return false;
        if (filters.onlyWithdrawals && tx.amount >= 0) return false;
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

    // Reset on filter/account change
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

    // IntersectionObserver, only active while expanded
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
        <div className="p-4 flex-1">
            <h3 className="font-semibold text-sm mb-1 border-b-2 border-nordiska-orange">{t("transactions-route.transactions")}</h3>
            <p className="text-xs text-gray-500 mb-3">
                Visar {visibleItems.length} av {allMatches.length} transaktioner
            </p>

            <div
                ref={scrollContainerRef}
                className={expanded ? 'max-h-[60%] overflow-y-auto' : ''}
            >
                {groupedItems.map(group => (
                    <div key={group.dateKey} className="mb-3">
                        <p className="text-xs font-medium text-gray-500 mb-1 border-b-2 border-gray-200">{group.dateKey}</p>
                        <ul className="divide-y divide-gray-100  px-3">
                            {group.items.map(tx => (
                                <li key={tx.id} className="py-3 flex justify-between items-start">
                                    <div>
                                        <p className="text-sm font-medium">{tx.title}</p>
                                        <p className="text-xs text-gray-500">{tx.accountLabel}</p>
                                    </div>
                                    <div className="text-right">
                                        <p className="text-xs text-gray-400">kl {tx.time}</p>
                                        <p className={`text-sm font-medium ${tx.amount < 0 ? 'text-red-600' : 'text-green-600'}`}>
                                            {tx.amount > 0 ? '+' : ''}
                                            {tx.amount.toLocaleString('sv-SE', { minimumFractionDigits: 2 })} sek
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
                    className="w-full border border-gray-300 rounded-md text-sm py-2 mt-3 hover:bg-gray-50"
                >
                    {t("transactions-route.all-transactions")}
                </button>
            )}

            {expanded && (
                <button
                    onClick={handleShowLessClick}
                    className="w-full border border-gray-300 rounded-md text-sm py-2 mt-3 hover:bg-gray-50"
                >
                    {t("transactions-route.less-transactions")}
                </button>
            )}
        </div>
    );
}
