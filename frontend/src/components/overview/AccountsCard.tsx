import { useTranslation } from "react-i18next";
import OverviewCard from "./OverviewCard";
import { useGetAccounts } from "../../hooks/useAccounts";
import { LoadingState, ErrorState } from "../StatusMessage";
import { useNavigate } from "react-router";
import { formatCurrencySmallSek } from "../../utils/currency";
import OverviewCardHeader from "./OverviewCardHeader";
import { OverviewCardFixedRow, OverviewCardRow } from "./OverviewCardRow";

export default function AccountsCard() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: accounts, isPending, isError } = useGetAccounts("active");
    const totalBalance = accounts?.reduce((sum, a) => sum + a.balance, 0) ?? 0;
    const displayedAccounts = [...(accounts ?? [])].sort((a, b) => Number(b.isFavorite) - Number(a.isFavorite));

    return (
    <OverviewCard>
        <div className="flex h-full flex-col">

            {/* CARD HEADER */}
            <OverviewCardHeader title={t("overview-route.account-card.title")} description={t("overview-route.account-card.description")} />

            {/* USER FEEDBACK */}
            {isPending && ( <LoadingState title={t("overview-route.account-card.pending")} />)}
            {isError && ( <ErrorState title={t("overview-route.account-card.error")} />)}

            {/* CONTENT */}
            {!isPending && !isError && (
                <section className="flex min-h-0 flex-1 flex-col ">
                    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">

                        {/* TOTAL RAD */}
                        <OverviewCardFixedRow 
                            cols={"2"}
                            hoverTitle={t("overview-route.account-card.hover-total")}
                            titleRow={[t("generic.balance"), formatCurrencySmallSek(totalBalance)]}
                            descriptionRow={[t("generic.total"), t("generic.amount")]} 
                        />

                        {/* KORT RADER */}
                        <div className="min-h-0 flex-1 overflow-y-auto">
                            {displayedAccounts?.map((account) => {

                                const displayName = account.accountName ? account.accountName : t(`accounts-route.account-type-${account.accountType}`);

                                return (
                                    <OverviewCardRow 
                                        key={account.id}
                                        btnHoverText={`${t("overview-route.account-card.hover-title")} ${account.accountNumber}`}
                                        onClick={() => navigate(`/accounts?account=${account.id}`)}
                                        cols={"2"}
                                        titleHoverText={displayName}
                                        titleRow={[displayName, formatCurrencySmallSek(account.balance)]}
                                        accountFavorite={account.isFavorite}
                                        descriptionRow={[account.accountNumber, t(`accounts-route.account-type-${account.accountType}`)]}
                                    />
                                )
                            })}
                        </div>
                    </div>
                </section>
            )}
        </div>
    </OverviewCard>
    );
}