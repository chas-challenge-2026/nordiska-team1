import { useEffect, useState } from 'react';
import TransactionTable from '../components/transactions/TransactionsTable';
import AccountSelector from '../components/transactions/AccountSelector';
import TransactionFilter from '../components/transactions/TransactionsFilter';
import type { TransactionFilters } from '../components/transactions/TransactionsFilter';
import Modal from '../components/modals/Modal';
import { useTransactions } from '../hooks/useTransactions';
import { useAccounts } from '../hooks/useAccounts';

const initialFilters: TransactionFilters = {
    search: '',
    dateFrom: '',
    dateTo: '',
    onlyDeposits: false,
    onlyWithdrawals: false,
};

export default function TransactionsPage() {
    const { data: transactions, isLoading: transactionsLoading, isError: transactionsError } = useTransactions();
    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useAccounts();

    const [selectedAccountIds, setSelectedAccountIds] = useState<number[]>([]);
    useEffect(() => {
        if (accounts) {
            setSelectedAccountIds(accounts.map(a => a.id));
        }
    }, [accounts])
    const [filters, setFilters] = useState<TransactionFilters>(initialFilters);
    const [isFilterModalOpen, setIsFilterModalOpen] = useState(false);

    return (
        <div className='flex flex-col md:flex-row gap-8 w-screen max-h-screen p-6 font-montserrat'>
            <div className='hidden md:block'>
                <TransactionFilter onChange={setFilters} onReset={() => setFilters(initialFilters)} />
            </div>

            <button
                type='button'
                onClick={() => setIsFilterModalOpen(true)}
                className='md:hidden self-start rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium'
            >
                Filters & Accounts
            </button>

            <TransactionTable
                transactions={transactions ?? []}
                selectedAccountIds={selectedAccountIds}
                filters={filters}
            />

            <div className='hidden md:block'>
                <AccountSelector
                    accounts={accounts ?? []}
                    selectedIds={selectedAccountIds}
                    onChange={setSelectedAccountIds}
                />
            </div>

            {isFilterModalOpen && (
                <Modal
                    title='Filters & Accounts'
                    onClose={() => setIsFilterModalOpen(false)}
                    widthClassName='w-[90vw]'
                >
                    <div className='flex flex-col gap-4 overflow-y-auto p-6'>
                        <TransactionFilter onChange={setFilters} onReset={() => setFilters(initialFilters)} />
                        <AccountSelector
                            accounts={accounts ?? []}
                            selectedIds={selectedAccountIds}
                            onChange={setSelectedAccountIds}
                        />
                    </div>
                </Modal>
            )}
        </div>
    );
}
