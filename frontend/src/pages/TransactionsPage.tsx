import { useState, useRef, useEffect } from 'react';
import TransactionTable from '../components/transactions/TransactionsTable';
import AccountSelector from '../components/transactions/AccountSelector';
import TransactionFilter from '../components/transactions/TransactionsFilter';
import type { TransactionFilters } from '../components/transactions/TransactionsFilter';
import Modal from '../components/modals/Modal';
import { useTransactions } from '../hooks/useTransactions';
import type { TransactionParams } from '../services/transactionsService';
import { useGetAccounts } from '../hooks/useAccounts';

const initialFilters: TransactionFilters = {
    search: '',
    dateFrom: '',
    dateTo: '',
    onlyDeposits: false,
    onlyWithdrawals: false,
};

const PAGE_SIZE = 6;
const SEARCH_DEBOUNCE_MS = 300;

function startOfLocalDay(date: string): string {
    return new Date(`${date}T00:00:00`).toISOString();
}

function endOfLocalDay(date: string): string {
    return new Date(`${date}T23:59:59.999`).toISOString();
}

export default function TransactionsPage() {
    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts();

    const [selectedAccountIds, setSelectedAccountIds] = useState<number[] | null>(null);
    const effectiveSelectedIds = selectedAccountIds ?? accounts?.map(a => a.id) ?? [];

    const [filters, setFilters] = useState<TransactionFilters>(initialFilters);
    const [isFilterModalOpen, setIsFilterModalOpen] = useState(false);
    const [page, setPage] = useState(1);

    const [debouncedSearch, setDebouncedSearch] = useState("");
    const searchTimeoutRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

    useEffect(() => () => clearTimeout(searchTimeoutRef.current), []);

    const params: TransactionParams = {
        Page: page,
        PageSize: PAGE_SIZE,
        AccountIds: effectiveSelectedIds,
        SearchTerm: debouncedSearch || undefined,
        FromDate: filters.dateFrom ? startOfLocalDay(filters.dateFrom) : undefined,
        ToDate: filters.dateTo ? endOfLocalDay(filters.dateTo) : undefined,
        Type: filters.onlyDeposits ? "deposit" : filters.onlyWithdrawals ? "withdrawal" : undefined,
    };

    const { data: transactions, isLoading: transactionsLoading, isError: transactionsError, isPlaceholderData } = useTransactions(params, !!accounts && effectiveSelectedIds.length > 0);

    const hasNextPage = transactions?.hasNextPage ?? false;

    const handleFiltersChange = (next: TransactionFilters) => {
        setFilters(next);

        if (next.search !== filters.search) {
            clearTimeout(searchTimeoutRef.current);
            searchTimeoutRef.current = setTimeout(() => {
                setDebouncedSearch(next.search.trim());
                setPage(1);
            }, SEARCH_DEBOUNCE_MS);
        } else {
            setPage(1);
        }
    };

    const handleFiltersReset = () => {
        clearTimeout(searchTimeoutRef.current);
        setFilters(initialFilters);
        setDebouncedSearch("");
        setPage(1);
    };

    const handleAccountsChange = (ids: number[]) => {
        setSelectedAccountIds(ids);
        setPage(1);
    };

    return (
        <div className='flex flex-col md:flex-row gap-8 w-full h-full min-h-0 p-6 font-montserrat'>
            <div className='hidden md:block'>
                <TransactionFilter filters={filters} onChange={handleFiltersChange} onReset={handleFiltersReset} />
            </div>

            <button
                type='button'
                onClick={() => setIsFilterModalOpen(true)}
                className='md:hidden self-start rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium'
            >
                Filters & Accounts
            </button>

            <TransactionTable
                transactions={transactions?.items ?? []}
                totalCount={transactions?.totalCount ?? 0}
                totalPages={transactions?.totalPages ?? 1}
                page={page}
                hasNextPage={hasNextPage}
                isPlaceholderData={isPlaceholderData}
                onPageChange={setPage}
                isLoading={transactionsLoading || accountsLoading}
                isError={transactionsError}
            />

            <div className='hidden md:block'>
                <AccountSelector
                    accounts={accounts ?? []}
                    selectedIds={effectiveSelectedIds}
                    onChange={handleAccountsChange}
                    isLoading={accountsLoading}
                    isError={accountsError}
                />
            </div>

            <Modal
                title='Filters & Accounts'
                isOpen={isFilterModalOpen}
                onClose={() => setIsFilterModalOpen(false)}
                widthClassName='w-[90vw]'
            >
                <div className='flex flex-col gap-4 overflow-y-auto p-6'>
                    <TransactionFilter filters={filters} onChange={handleFiltersChange} onReset={handleFiltersReset} />
                    <AccountSelector
                        accounts={accounts ?? []}
                        selectedIds={effectiveSelectedIds}
                        onChange={handleAccountsChange}
                        isLoading={accountsLoading}
                        isError={accountsError}
                    />
                </div>
            </Modal>
        </div>
    );
}
