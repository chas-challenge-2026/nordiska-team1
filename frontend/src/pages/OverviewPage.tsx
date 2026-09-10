import { createAccount, getAccounts } from "../services/accountService"

export default function OverviewPage() {
    createAccount("asds", "savings_account", 1)
    getAccounts();
    return (
        <p className="text-3xl">Overview placeholder</p>
    )
}
