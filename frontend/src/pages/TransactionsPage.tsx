import { useState, useRef, useEffect, type ReactNode } from 'react';
import TransactionTable from '../components/transactions/TransactionsTable';
import AccountSelector from '../components/transactions/AccountSelector';
import TransactionFilter from '../components/transactions/TransactionsFilter';
import type { TransactionFilters } from '../components/transactions/TransactionsFilter';
import Modal from '../components/modals/Modal';
import { useTransactions } from '../hooks/useTransactions';
import type { TransactionParams } from '../services/transactionsService';
import { useGetAccounts } from '../hooks/useAccounts';
import { useTranslation } from 'react-i18next';
import { useIsDesktop } from '../hooks/useIsDesktop';

const initialFilters: TransactionFilters = {
    search: '',
    dateFrom: '',
    dateTo: '',
    type: 'all',
};

const PAGE_SIZE_MOBILE = 6;
const PAGE_SIZE_DESKTOP = 10;
const SEARCH_DEBOUNCE_MS = 300;

function startOfLocalDay(date: string): string {
    return new Date(`${date}T00:00:00`).toISOString();
}

function endOfLocalDay(date: string): string {
    return new Date(`${date}T23:59:59.999`).toISOString();
}

function SheetHeader({ title, meta }: { title: string; meta?: string }) {
    return (
        <div className='flex items-baseline justify-between border-b-2 border-nordiska-orange pb-1.5'>
            <h2 className='text-base font-semibold'>{title}</h2>
            {meta && <span className='text-xs text-secondary'>{meta}</span>}
        </div>
    );
}

function SheetTrigger({ label, count, onClick, children }: { label: string; count: number; onClick: () => void; children: ReactNode }) {
    return (
        <button
            type='button'
            onClick={onClick}
            aria-label={label}
            className={`relative grid h-10 w-10 shrink-0 place-items-center rounded-lg border ${count > 0 ? 'border-nordiska-blue bg-blue-50' : 'border-gray-300 bg-white'}`}
        >
            {children}
            {count > 0 && (
                <span aria-hidden='true' className='absolute -top-1.5 -right-1.5 grid h-[18px] min-w-[18px] place-items-center rounded-full border-2 border-white bg-nordiska-orange px-1 text-[10px] font-bold text-white'>
                    {count}
                </span>
            )}
        </button>
    );
}

export default function TransactionsPage() {
    const { t } = useTranslation();
    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts();

    const [selectedAccountIds, setSelectedAccountIds] = useState<number[] | null>(null);
    const effectiveSelectedIds = selectedAccountIds ?? accounts?.map(a => a.id) ?? [];

    const [filters, setFilters] = useState<TransactionFilters>(initialFilters);
    const [openSheet, setOpenSheet] = useState<'filter' | 'accounts' | null>(null);
    const closeSheet = () => setOpenSheet(null);
    const [page, setPage] = useState(1);

    const isDesktop = useIsDesktop();
    const pageSize = isDesktop ? PAGE_SIZE_DESKTOP : PAGE_SIZE_MOBILE;

    const [prevPageSize, setPrevPageSize] = useState(pageSize);
    if (pageSize !== prevPageSize) {
        setPrevPageSize(pageSize);
        setPage(1);
    }

    const [debouncedSearch, setDebouncedSearch] = useState("");
    const searchTimeoutRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);

    useEffect(() => () => clearTimeout(searchTimeoutRef.current), []);

    const params: TransactionParams = {
        Page: page,
        PageSize: pageSize,
        AccountIds: effectiveSelectedIds,
        SearchTerm: debouncedSearch || undefined,
        FromDate: filters.dateFrom ? startOfLocalDay(filters.dateFrom) : undefined,
        ToDate: filters.dateTo ? endOfLocalDay(filters.dateTo) : undefined,
        Type: filters.type === 'all' ? undefined : filters.type,
    };

    const { data: transactions, isLoading: transactionsLoading, isError: transactionsError, isPlaceholderData } = useTransactions(params, !!accounts && effectiveSelectedIds.length > 0);

    const hasNextPage = transactions?.hasNextPage ?? false;

    const totalCount = transactions?.totalCount ?? 0;
    const activeFilterCount = [filters.dateFrom, filters.dateTo, filters.type !== 'all'].filter(Boolean).length;
    const accountBadge = accounts && effectiveSelectedIds.length < accounts.length ? effectiveSelectedIds.length : 0;

    const handleSheetReset = () => {
        setFilters(f => ({ ...initialFilters, search: f.search }));
        setPage(1);
    };

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
        <div className='flex flex-1 flex-col md:flex-row gap-2 w-full min-h-0 p-6 font-montserrat bg-light-gray max-md:h-[calc(100dvh-120px)]'>
            <div className='hidden md:block'>
                <TransactionFilter filters={filters} onChange={handleFiltersChange} onReset={handleFiltersReset} />
            </div>

            <div className='flex gap-2 md:hidden'>
                <input
                    type='search'
                    value={filters.search}
                    onChange={e => handleFiltersChange({ ...filters, search: e.target.value })}
                    placeholder={t("transactions-route.search")}
                    aria-label={t("transactions-route.search")}
                    className='h-10 min-w-0 flex-1 rounded-lg border border-gray-300 px-3 text-sm'
                />
                <SheetTrigger
                    label={t("transactions-route.filter-aria", { count: activeFilterCount })}
                    count={activeFilterCount}
                    onClick={() => setOpenSheet('filter')}
                >
                    <img src='/icons/filter.svg' alt='' className='h-[18px] w-[18px]' />
                </SheetTrigger>
                <SheetTrigger
                    label={t("transactions-route.accounts-aria", { count: effectiveSelectedIds.length })}
                    count={accountBadge}
                    onClick={() => setOpenSheet('accounts')}
                >
                    <img src='/icons/bank-card.svg' alt='' className='h-[18px] w-[18px]' />
                </SheetTrigger>
            </div>

            <TransactionTable
                transactions={transactions?.items ?? []}
                totalCount={transactions?.totalCount ?? 0}
                totalPages={transactions?.totalPages ?? 1}
                emptyMessage={effectiveSelectedIds.length === 0 ? t("transactions-route.select-account") : t("transactions-route.no-transactions")}
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
                title={t("generic.filter")}
                isOpen={openSheet === 'filter'}
                onClose={closeSheet}
                showCloseButton={false}
                header={
                    <SheetHeader
                        title={t("generic.filter")}
                        meta={activeFilterCount > 0 ? t("transactions-route.active-count", { count: activeFilterCount }) : undefined}
                    />
                }
                footer={
                    <>
                        <button type='button' onClick={handleSheetReset} className='px-2 py-2.5 text-sm font-semibold text-primary-blue'>
                            {t("generic.reset")}
                        </button>
                        <button type='button' onClick={closeSheet} className='flex-1 rounded-lg bg-primary-blue py-3 text-sm font-semibold text-white'>
                            {t("transactions-route.show-count", { count: totalCount })}
                        </button>
                    </>
                }
            >
                <TransactionFilter filters={filters} onChange={handleFiltersChange} variant='sheet' />
            </Modal>

            <Modal
                title={t("generic.account")}
                isOpen={openSheet === 'accounts'}
                onClose={closeSheet}
                showCloseButton={false}
                header={
                    <SheetHeader
                        title={t("generic.account")}
                        meta={t("transactions-route.selected-of", { selected: effectiveSelectedIds.length, total: accounts?.length ?? 0 })}
                    />
                }
                footer={
                    <>
                        <button type='button' onClick={() => handleAccountsChange([])} className='px-2 py-2.5 text-sm font-semibold text-primary-blue'>
                            {t("transactions-route.deselect-all")}
                        </button>
                        <button type='button' onClick={closeSheet} className='flex-1 rounded-lg bg-primary-blue py-3 text-sm font-semibold text-white'>
                            {t("transactions-route.show-count", { count: totalCount })}
                        </button>
                    </>
                }
            >
                <AccountSelector
                    accounts={accounts ?? []}
                    selectedIds={effectiveSelectedIds}
                    onChange={handleAccountsChange}
                    isLoading={accountsLoading}
                    isError={accountsError}
                    variant='sheet'
                />
            </Modal>
        </div>
    );
}
