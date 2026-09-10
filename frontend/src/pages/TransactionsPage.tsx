import { useState } from 'react';
import { accounts } from '../components/transactions/mockTransactions';
import TransactionTable from '../components/transactions/TransactionsTable';
import AccountSelector from '../components/transactions/AccountSelector';
import TransactionFilter from '../components/transactions/TransactionsFilter';
import type { TransactionFilters } from '../components/transactions/TransactionsFilter';

const initialFilters: TransactionFilters = {
    search: '',
    dateFrom: '',
    dateTo: '',
    onlyDeposits: false,
    onlyWithdrawals: false,
};

export default function TransactionsPage() {
    const [selectedAccountIds, setSelectedAccountIds] = useState<string[]>(
        accounts.map(a => a.id)
    );
    const [filters, setFilters] = useState<TransactionFilters>(initialFilters);

    return (
        <div className='flex gap-8 w-screen max-h-screen p-6 font-montserrat'>
            <TransactionFilter onChange={setFilters} onReset={() => setFilters(initialFilters)} />

            <TransactionTable
                selectedAccountIds={selectedAccountIds}
                filters={filters}
            />

            <AccountSelector
                accounts={accounts}
                selectedIds={selectedAccountIds}
                onChange={setSelectedAccountIds}
            />
        </div>
    );
}
