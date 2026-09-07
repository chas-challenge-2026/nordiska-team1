import type { Account } from './mockTransactions';
import { useTranslation } from "react-i18next";

interface AccountSelectorProps {
    accounts: Account[];
    selectedIds: string[];
    onChange: (ids: string[]) => void;
}

export default function AccountSelector({ accounts, selectedIds, onChange }: AccountSelectorProps) {
    const { t } = useTranslation();
    const allSelected = selectedIds.length === accounts.length;

    const toggleAll = () => {
        onChange(allSelected ? [] : accounts.map(a => a.id));
    };

    const toggleOne = (id: string) => {
        onChange(
            selectedIds.includes(id)
                ? selectedIds.filter(x => x !== id)
                : [...selectedIds, id]
        );
    };

    return (
        <div className="bg-white p-4 w-64">
            <h3 className="font-semibold text-sm mb-1 border-b-1 border-nordiska-orange">{t("generic.account")}</h3>
            <p className="text-xs text-gray-500 mb-3">
                {t("transactions-route.account-select")}
            </p>

            <label className="flex items-center gap-2 text-sm font-medium mb-2">
                <input type="checkbox" checked={allSelected} onChange={toggleAll} />
                {t("transactions-route.all-accounts")}
            </label>

            <ul className="space-y-2">
                {accounts.map(acc => (
                    <li key={acc.id}>
                        <label className="flex items-start gap-2 text-sm">
                            <input
                                type="checkbox"
                                checked={selectedIds.includes(acc.id)}
                                onChange={() => toggleOne(acc.id)}
                                className="mt-0.5"
                            />
                            <span>
                                {acc.name}
                                <br />
                                <span className="text-gray-500">
                                    {acc.balance.toLocaleString('sv-SE', { minimumFractionDigits: 2 })} sek
                                </span>
                            </span>
                        </label>
                    </li>
                ))}
            </ul>

            <p className="text-xs text-gray-500 mt-4 mb-2">
                {t("transactions-route.tax-info")}
            </p>
            <button className="w-full border border-gray-300 rounded-md text-sm py-2 hover:bg-gray-50">
                {t("transactions-route.tax")}
            </button>
        </div>
    );
}
