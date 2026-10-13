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

function transactionTypeLabel(transaction: Transaction, t: TFunction): string {
    if (transaction.type === "transfer") return t("transactions-route.type-transfer");
    if (transaction.type === "deposit") return t("transactions-route.type-deposit");
    if (transaction.type === "withdrawal") return t("transactions-route.type-withdraw");
    // Okända typer (t.ex. ränta) märks efter beloppets tecken
    return t(transaction.amount >= 0 ? "transactions-route.type-deposit" : "transactions-route.type-withdraw");
}

export default function TransactionTable({ accounts, transactions, groups, totalCount, totalPages, page, hasNextPage, hasPreviousPage, isPlaceholderData, onPageChange, isLoading, isError, emptyMessage }: TransactionTableProps) {
    const { t } = useTranslation();
    const listId = useId();
    const accountsById = useMemo(() => new Map(accounts.map(a => [a.id, a])), [accounts])

    return (
        <section aria-labelledby={`${listId}-heading`} className="flex min-w-0 flex-col overflow-hidden bg-white text-dark-navy md:border md:border-gray-200 md:shadow-sm">
            {/* CARD HEADER */}
            <div className="flex min-h-22 flex-col justify-center bg-dark-navy px-4 py-3 text-white sm:px-6">
                <p className="text-[10px] font-medium uppercase tracking-wider text-white/90 sm:text-xs" aria-live="polite">
                    {!isLoading && !isError && t("transactions-route.showing-count", { shown: transactions.length, count: totalCount })}
                </p>
                <h2 id={`${listId}-heading`} className="mt-0.5 truncate text-base font-bold uppercase tracking-wide sm:mt-1 sm:text-lg">{t("transactions-route.list-title")}</h2>
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
                    {transactions.length === 0 && emptyMessage && (
                        <p className="py-10 text-center text-sm text-secondary">{emptyMessage}</p>
                    )}
                    {groups.map(group => (
                        <div key={group.dateKey} className="mb-4 last:mb-0">
                            <h3 className="border border-nordiska-blue/50 bg-primary-blue/10 px-3 py-2 text-xs font-medium uppercase tracking-wide text-nordiska-blue sm:px-5">
                                {formatDate(`${group.date}T00:00:00`)}
                            </h3>
                            <ul>
                                {group.items.map(transaction => {
                                    const account = accountsById.get(transaction.accountId);
                                    return (
                                        <li key={transaction.id} className="border-b-2 border-nordiska-orange px-3 py-3 odd:bg-white even:bg-light-gray/80 last:border-0 sm:px-5">
                                            <p className="flex items-baseline justify-between gap-2 text-sm sm:text-base">
                                                <span className="min-w-0 wrap-break-word font-semibold">{transaction.label || transactionTypeLabel(transaction, t)}</span>
                                                <span className={`shrink-0 whitespace-nowrap font-medium ${transaction.amount < 0 ? 'text-red-700' : 'text-green-700'}`}>
                                                    {formatCurrency(transaction.amount, { signed: true })}
                                                </span>
                                            </p>
                                            <p className="mt-0.5 flex flex-wrap justify-between gap-x-2 gap-y-1 text-[10px] uppercase text-dark-navy/60 sm:text-xs">
                                                <span>{formatTime(transaction.createdAt)}</span>
                                                {account && (
                                                    <span className="min-w-0 wrap-break-word text-right">
                                                        {account.accountNumber} • {account.accountName || t(`accounts-route.account-type-${account.accountType}`)}
                                                    </span>
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
