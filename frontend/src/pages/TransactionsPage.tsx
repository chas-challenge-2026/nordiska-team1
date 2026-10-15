import { useState, useRef, useEffect, useMemo, type ReactNode } from 'react';
import TransactionTable from '../components/transactions/TransactionsTable';
import AccountSelector from '../components/transactions/AccountSelector';
import TransactionFilter from '../components/transactions/TransactionsFilter';
import type { TransactionFilters } from '../components/transactions/TransactionsFilter';
import Modal from '../components/modals/Modal';
import { useTransactionRows } from '../hooks/useTransactions';
import { useGetAccounts } from '../hooks/useAccounts';
import { useTranslation } from 'react-i18next';
import { useIsDesktop } from '../hooks/useIsDesktop';
import HelpCard from '../components/faq/HelpCard';
import HelpCardBubble from '../components/faq/HelpCardBubble';

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
            <h2 className='text-base font-semibold text-dark-navy'>{title}</h2>
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
            className={`relative grid h-10 w-10 shrink-0 cursor-pointer place-items-center border ${count > 0 ? 'border-primary-blue/40 bg-primary-blue/10' : 'border-gray-200 bg-white hover:bg-primary-blue/10'}`}
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
    // Alla konton hämtas så att transaktioner från stängda konton fortfarande kan visas
    const { data: allAccounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts();

    // Stängda konton döljs som standard men kan visas via kontoväljaren
    const [showClosed, setShowClosed] = useState(false);
    const accounts = showClosed ? allAccounts : allAccounts?.filter(a => a.status === "active");
    const closedAccountIds = useMemo(
        () => new Set(allAccounts?.filter(a => a.status === "closed").map(a => a.id)),
        [allAccounts],
    );

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

    const transactionFilters = {
        AccountIds: effectiveSelectedIds,
        // Planerade transaktioner visas först när de har genomförts
        IsPlanned: false,
        SearchTerm: debouncedSearch || undefined,
        FromDate: filters.dateFrom ? startOfLocalDay(filters.dateFrom) : undefined,
        ToDate: filters.dateTo ? endOfLocalDay(filters.dateTo) : undefined,
        Type: filters.type === 'all' ? undefined : filters.type,
    };

    const {
        groups, shownCount, totalCount, totalPages, hasNextPage, hasPreviousPage,
        isLoading: transactionsLoading, isError: transactionsError, isPlaceholderData,
    } = useTransactionRows(transactionFilters, page, pageSize, closedAccountIds, !!accounts && effectiveSelectedIds.length > 0);

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

    const handleShowClosedChange = (show: boolean) => {
        setShowClosed(show);
        // Stängda konton läggs till eller tas bort ur urvalet, resten av urvalet behålls
        const withoutClosed = effectiveSelectedIds.filter(id => !closedAccountIds.has(id));
        setSelectedAccountIds(show ? [...withoutClosed, ...closedAccountIds] : withoutClosed);
        setPage(1);
    };

    return (
        <main className='min-h-screen w-full font-montserrat text-dark-navy'>
            <div className='relative mx-auto w-full max-w-7xl px-2 pt-4 sm:px-6 sm:py-8 lg:px-1 lg:py-10'>
                {/* Sidrubriken visas inte men behålls för skärmläsare */}
                <h1 className='sr-only'>{t("transactions-route.page-title")}</h1>

                {/* MOBIL: SÖK + FILTER/KONTO-KNAPPAR */}
                <div className='mb-4 flex gap-2 lg:hidden'>
                    <input
                        type='search'
                        value={filters.search}
                        onChange={e => handleFiltersChange({ ...filters, search: e.target.value })}
                        placeholder={t("transactions-route.search")}
                        aria-label={t("transactions-route.search")}
                        className='h-10 min-w-0 flex-1 border border-primary-blue/40 bg-white px-3 text-sm'
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

                <div className='grid gap-5 lg:grid-cols-[220px_1fr_220px] xl:grid-cols-[280px_1fr_280px]'>
                    {/* DESKTOP: FILTER */}
                    <div className='hidden min-w-0 self-start lg:block'>
                        <TransactionFilter filters={filters} onChange={handleFiltersChange} onReset={handleFiltersReset} />
                    </div>

                    {/* TRANSAKTIONSLISTA */}
                    <TransactionTable
                        accounts={allAccounts ?? []}
                        groups={groups}
                        shownCount={shownCount}
                        totalCount={totalCount}
                        totalPages={totalPages}
                        emptyMessage={effectiveSelectedIds.length === 0 ? t("transactions-route.select-account") : t("transactions-route.no-transactions")}
                        page={page}
                        hasNextPage={hasNextPage}
                        hasPreviousPage={hasPreviousPage}
                        isPlaceholderData={isPlaceholderData}
                        onPageChange={setPage}
                        isLoading={transactionsLoading || accountsLoading}
                        isError={transactionsError}
                    />

                    {/* DESKTOP: KONTON + HJÄLPCENTER */}
                    <div className='hidden min-w-0 flex-col gap-5 self-start lg:flex'>
                        <AccountSelector
                            accounts={accounts ?? []}
                            selectedIds={effectiveSelectedIds}
                            onChange={handleAccountsChange}
                            isLoading={accountsLoading}
                            isError={accountsError}
                            showClosed={showClosed}
                            onShowClosedChange={handleShowClosedChange}
                        />

                        <aside aria-label={t("generic.help")} className='hidden min-w-0 border border-gray-200 bg-white md:shadow-sm xl:block'>
                            <div className='min-h-22 items-center justify-between bg-nordiska-blue px-4 py-3 text-white'>
                                <p className='mt-2 truncate text-right text-sm uppercase text-white/80'>{t("generic.help-center")}</p>
                                <p className='mt-1 truncate text-right text-md font-semibold'>{t("generic.related-articles")}</p>
                            </div>

                            <div className='p-4'>
                                <HelpCard searchTerms={t("transactions-route.help-card-search")} removeHeading numOfHits={12} />
                            </div>
                        </aside>
                    </div>
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
                        showClosed={showClosed}
                        onShowClosedChange={handleShowClosedChange}
                        variant='sheet'
                    />
                </Modal>

                <HelpCardBubble
                    searchTerms={t("transactions-route.help-card-search")}
                    numOfHits={6}
                />
            </div>
        </main>
    );
}
