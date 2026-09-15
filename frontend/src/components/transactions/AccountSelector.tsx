import { type Account } from "../../services/accountsService";
import { useTranslation } from "react-i18next";

interface AccountSelectorProps {
    accounts: Account[];
    selectedIds: number[];
    onChange: (ids: number[]) => void;
    isLoading: boolean;
    isError: boolean;
}

export default function AccountSelector({ accounts, selectedIds, onChange, isLoading, isError }: AccountSelectorProps) {
    const { t } = useTranslation();
    const allSelected = selectedIds.length === accounts.length;

    const toggleAll = () => {
        onChange(allSelected ? [] : accounts.map(a => a.id));
    };

    const toggleOne = (id: number) => {
        onChange(
            selectedIds.includes(id)
                ? selectedIds.filter(x => x !== id)
                : [...selectedIds, id]
        );
    };

    return (
        <div className="bg-white p-4 w-64">
            <h3 className="font-semibold text-sm mb-1 border-b-1 border-nordiska-orange">{t("generic.account")}</h3>
            <p className="text-xs text-secondary mb-3">
                {t("transactions-route.account-select")}
            </p>
            {isLoading && <p className="text-sm text-secondary">{t("transactions-route.loading-accounts")}Loading accounts...</p>}
            {isError && <p className="text-sm text-red-700">{t("transactions-route.accounts-error")}Error while loading accounts</p>}

            {!isLoading && !isError && (
                <>
                    <label className="flex items-center gap-2 text-sm font-medium mb-2">
                        <input type="checkbox" checked={allSelected} onChange={toggleAll} />
                        {t("transactions-route.all-accounts")}
                    </label>

                    <fieldset>
                        <legend className="sr-only">{t("transactions-route.account-select")}</legend>
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
                                        <span className="flex flex-col">
                                            <span>{acc.accountNumber}</span>
                                            <span className="text-secondary">
                                                {acc.balance.toLocaleString('sv-SE', { minimumFractionDigits: 2 })} sek
                                            </span>
                                        </span>
                                    </label>
                                </li>
                            ))}
                        </ul>
                    </fieldset>

                    <p className="text-xs text-secondary mt-4 mb-2">
                        {t("transactions-route.tax-info")}
                    </p>
                    <button className="w-full border border-gray-300 rounded-md text-sm py-2 hover:bg-nordiska-blue bg-primary-blue text-white font-semibold">
                        {t("transactions-route.tax")}
                    </button>
                </>
            )}
        </div>
    );
}
