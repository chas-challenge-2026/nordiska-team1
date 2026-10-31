import { useTranslation } from "react-i18next";
import { useNavigate } from "react-router";
import OverviewCard from "./OverviewCard";
import { useGetAccounts } from "../../hooks/useAccounts";
import { LoadingState, ErrorState } from "../StatusMessage";
import { formatCurrencySmallSek } from "../../utils/currency";
import OverviewCardHeader from "./OverviewCardHeader";
import { OverviewCardRow } from "./OverviewCardRow";
import type { OverviewCardProps } from "../../pages/OverviewPage";

export default function InterestCard({ showOnlyFavs }: OverviewCardProps) {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const { data: accounts, isPending, isError } = useGetAccounts("active");
    const displayedAccounts = (accounts ?? []).filter((account) => !showOnlyFavs || account.isFavorite);

    return (
    <OverviewCard>
        <div className="flex h-full flex-col ">
            {/* CARD HEADER */}
            <OverviewCardHeader title={t("generic.interest")} description={t("overview-route.interest-card.description")} />

            {/* USER FEEDBACK */}
            {isPending && ( <LoadingState title={t("overview-route.interest-card.pending")}  />)}
            {isError && ( <ErrorState title={t("overview-route.interest-card.error")} />)}

            {/* CONTENT ROWS */}
            {!isPending && !isError && (
                <section className="flex min-h-0 flex-1 flex-col ">
                    <div className="flex min-h-0 flex-1 flex-col overflow-hidden">

                        {/* KORT RADER */}
                            <div className="min-h-0 flex-1 overflow-y-auto">
                                {displayedAccounts?.map((account) => {

                                    const displayName = account.accountName ? account.accountName : t(`accounts-route.account-type-${account.accountType}`);
                                    const displayInterest = <>{t("generic.interest")} {(account.interestRate * 100).toLocaleString("sv-SE")}<span className="font-normal"> %</span> </>

                                    return (
                                        <OverviewCardRow 
                                            key={account.id}
                                            btnHoverText={`${t("overview-route.account-card.hover-title")} ${account.accountNumber}`}
                                            onClick={() => navigate(`/accounts?account=${account.id}`)}
                                            cols={"3"}
                                            titleHoverText={displayName}
                                            greenAmount={account.accruedInterestYtd > 0}
                                            titleRow={[displayName, formatCurrencySmallSek(account.accruedInterestYtd),formatCurrencySmallSek(account.estimatedYearEndInterest) ]}
                                            descriptionRow={[displayInterest, t("generic.accrued"), t("generic.forecast")]}
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