import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import OverviewCard from "./OverviewCard";
import Table from "../Table";
import TableRow from "../TableRow";
import StatusText from "./StatusText";
import { useGetAccounts } from "../../hooks/useAccounts";

export default function AccountsCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: accounts, isPending, isError } = useGetAccounts();

    return (
        <OverviewCard>
            <Table tableType="account" handleClick={() => navigate("/accounts")}>
                {isPending ? (
                    <StatusText text={t("accounts-route.loading-accounts")} />
                ) : isError ? (
                    <StatusText text={t("accounts-route.accounts-error")} isError />
                ) : !accounts?.length ? (
                    <StatusText text={t("accounts-route.no-accounts")} />
                ) : (
                    accounts.map((account) => (
                        <TableRow
                            key={account.id}
                            rowType="account"
                            accountType={account.accountType}
                            accountNumber={account.accountNumber}
                            accountInterest={account.interestRate}
                            accountName={account.accountName}
                            accountBalance={account.balance}
                        />
                    ))
                )}
            </Table>
        </OverviewCard>
    );
}
