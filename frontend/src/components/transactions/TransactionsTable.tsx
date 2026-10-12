import { useMemo, useId } from 'react';
import { useTranslation } from 'react-i18next';
import type { TFunction } from 'i18next';
import type { Transaction, TransactionDateGroup } from '../../services/transactionsService';
import type { Account } from '../../services/accountsService';
import { formatDate, formatTime } from '../../utils/date';
import { formatCurrency } from '../../utils/currency';
import PagePagination from '../PagePagination';

interface TransactionTableProps {
    accounts: Account[];
    transactions: Transaction[];
    /** Transaktionerna grupperade per datum av backend (GroupByDate) */
    groups: TransactionDateGroup[];
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

function transactionTypeLabel(type: Transaction["type"], t: TFunction): string {
    return t(type === "deposit" ? "transactions-route.type-deposit" : "transactions-route.type-withdraw");
}

export default function TransactionTable({ accounts, transactions, groups, totalCount, totalPages, page, hasNextPage, hasPreviousPage, isPlaceholderData, onPageChange, isLoading, isError, emptyMessage }: TransactionTableProps) {
    const { t } = useTranslation();
    const listId = useId();
    const accountsById = useMemo(() => new Map(accounts.map(a => [a.id, a])), [accounts])

    return (
        <div className="p-4 flex-1 min-h-0 flex flex-col md:rounded-xl md:mb-10 md:shadow-md md:border md:border-secondary md:bg-white">
            <h2 className="min-w-0 wrap-break-word text-xl font-semibold sm:text-[26px] border-b-3 border-b-nordiska-orange mb-2">{t("transactions-route.transactions")}</h2>

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
                        {transactions.length === 0 && emptyMessage && (
                            <p className="py-6 text-center text-sm text-secondary">{emptyMessage}</p>
                        )}
                        {groups.map(group => (
                            <div key={group.dateKey} className="mb-3">
                                <h4 className="text-xs font-medium text-secondary mb-1 border-b-2 border-gray-200">
                                    {formatDate(`${group.date}T00:00:00`)}
                                </h4>
                                <ul className="divide-y divide-gray-100 px-3">
                                    {group.items.map(transaction => (
                                        <li key={transaction.id} className="py-3 flex justify-between items-start">
                                            <div>
                                                <p className="text-sm font-medium">{transaction.label || transactionTypeLabel(transaction.type, t)}</p>
                                                {(() => {
                                                    const account = accountsById.get(transaction.accountId);
                                                    return account ? (
                                                        <p className="text-xs text-secondary">
                                                            {account.accountName} | {account.accountNumber}
                                                        </p>
                                                    ) : null;
                                                })()}
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
    );
}