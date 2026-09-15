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
        <div className="min-h-0 min-w-0 flex flex-1 flex-col gap-8 overflow-y-auto p-6 md:flex-row md:p-10">
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
