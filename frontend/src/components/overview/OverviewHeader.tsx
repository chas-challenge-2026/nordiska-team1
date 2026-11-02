import { useTranslation } from "react-i18next";
import { useState } from "react";
import { useNavigate } from "react-router";
import { useGetAccounts } from "../../hooks/useAccounts";
import { LoadingState, ErrorState } from "../StatusMessage";
import { formatCurrencySmallSek } from "../../utils/currency";
import CreateAccountModal from "../accounts/CreateAccountModal";
import { useInboxCounts } from "../../hooks/inbox/useInboxSummary";

type ButtonsProps = {
    onCreateAccount: () => void;
}

function Buttons({onCreateAccount}: ButtonsProps) {
    const { t } = useTranslation();

    return (
        <>
        {/* TAX REPORT  */}
        <button
            type="button"
            // TODO: FIXA KNAPP 
            aria-label={t("accounts-route.aria-label.new-account")}
            className=" inline-flex min-w-0 items-center justify-center rounded-lg bg-primary-blue px-2.5 py-1.5 text-xs font-semibold text-white transition hover:bg-primary-hover cursor-pointer sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm"
        >
            <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/file-pdf.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5"/>
            {t("accounts-route.full-report")}
        </button>

        {/* NEW ACCOUNT */}
        <button
            type="button"
            onClick={onCreateAccount}
            aria-label={t("accounts-route.aria-label.new-account")}
            className="inline-flex min-w-0 items-center justify-center rounded-lg bg-primary-blue px-2.5 py-1.5 text-xs font-semibold text-white transition hover:bg-primary-hover cursor-pointer sm:w-auto sm:flex-none sm:px-4 sm:py-2 sm:text-sm"
        >
            <span aria-hidden="true" className="mr-1.5 block h-4 w-4 bg-white mask-[url('/icons/plus.svg')] mask-contain mask-center mask-no-repeat sm:mr-2 sm:h-5 sm:w-5"/>
            {t("accounts-route.accounts-header.new-account")}
        </button>
        </>
    )
}

export default function OverviewHeader() {
    const { t } = useTranslation();
    const navigate = useNavigate();
    const [isCreateAccountOpen, setIsCreateAccountOpen] = useState(false);
    const { data: accounts, isLoading, isError } = useGetAccounts("active");
    const totalPrognos = accounts?.reduce((sum, a) => sum + a.estimatedYearEndInterest, 0) ?? 0;
    const totalEarned = accounts?.reduce((sum, a) => sum + a.accruedInterestYtd, 0) ?? 0;
    const totalBalance = accounts?.reduce((sum, a) => sum + a.balance, 0) ?? 0;
    function onCreateAccount() {setIsCreateAccountOpen(true);}
    const { data: counts } = useInboxCounts();
    const unreadCount = counts?.unreadCount ?? 0;

    return (
        <>
        <div className="w-full flex flex-col border border-gray-300 bg-white shadow-md/30 mb-5">

            <div className="h-16 shrink-0 bg-dark-navy text-white px-3 md:px-4 py-3 flex items-center ">
                <h2 className="w-fit text-xl sm:text-2xl font-bold tracking-wider text-light-blue-accent"> 
                    {t("page-navigation.overview")}
                </h2>

                { !isLoading && !isError && accounts?.length !== 0 &&
                    <div className="hidden min-[434px]:flex w-full justify-end gap-2 md:gap-5">
                        <Buttons onCreateAccount={onCreateAccount} />
                    </div>
                }
            </div>

            { (isLoading || isError) && 
                <div className="my-5">
                    {isLoading && (<LoadingState title={t("overview-route.loading")}/>)}
                    {isError && (<ErrorState title={t("overview-route.error")}/>)}
                </div>
            }

            {!isLoading && !isError && 
                <div className="bg-white p-3 pb-4 flex">
                    {/* DESKTOP */}
                    <div className="hidden min-[435px]:flex items-end gap-5 md:gap-8 lg:gap-12">
                        <div>
                            <p className="text-xs md:text-sm lg:text-base">{t("overview-route.account-card.total-balance")}</p>
                            <p className=" text-lg md:text-2xl lg:text-3xl font-bold"> {formatCurrencySmallSek(totalBalance, true)} </p>
                        </div>
                        <div>
                            <p className="text-xs lg:text-sm">{t("accounts-route.interest-section.accured-interest")}</p>
                            <p className="text-lg md:text-xl lg:text-2xl font-bold text-green-700"> {formatCurrencySmallSek(totalEarned, true)} </p>
                        </div>
                        <div>
                            <p className="text-xs lg:text-sm">{t("accounts-route.interest-section.yearly-interest")}</p>
                            <p className="text-lg md:text-xl lg:text-2xl font-bold"> {formatCurrencySmallSek(totalPrognos, true)} </p>
                        </div>
                    </div>
                    {/* MOBILE  */}
                    <div className="min-[435px]:hidden w-full flex flex-col gap-2">
                        <div className="flex justify-between">
                            <div>
                                <p className="text-xs md:text-sm lg:text-base">{t("overview-route.account-card.total-balance")}</p>
                                <p className=" text-lg md:text-2xl lg:text-3xl font-bold"> {formatCurrencySmallSek(totalBalance, true)} </p>
                            </div>
                        </div>
                        <div className="flex gap-10">
                            <div>
                                <p className="text-xs lg:text-sm">{t("accounts-route.interest-section.accured-interest")}</p>
                                <p className="text-lg md:text-xl lg:text-2xl font-bold text-green-700"> {formatCurrencySmallSek(totalEarned, true)} </p>
                            </div>
                            <div>
                                <p className="text-xs lg:text-sm">{t("accounts-route.interest-section.yearly-interest")}</p>
                                <p className="text-lg md:text-xl lg:text-2xl font-bold"> {formatCurrencySmallSek(totalPrognos, true)} </p>
                            </div>
                        </div>
                        <div className="flex gap-2 mt-2">
                            <Buttons onCreateAccount={onCreateAccount} />
                        </div>
                    </div>
                    {/* UNREAD */}
                    {unreadCount > 0 && (
                        <div className="flex items-end w-full justify-end hidden md:flex">
                            <div className="relative flex h-15 w-15 items-end justify-center scale-70 md:scale-80 lg:scale-100 md:-mb-1.5 lg:mb-0">
                                <span aria-hidden="true" className="lg:mb-0.5 h-10 w-10 bg-dark-navy mask-[url('/icons/inbox.svg')] mask-contain mask-center mask-no-repeat"/>
                                    <span aria-hidden="true" className="absolute right-2 top-3 lg:top-2.5 flex size-5 items-center justify-center rounded-full bg-nordiska-orange text-sm font-semibold text-dark-navy">
                                        {unreadCount > 99 ? "99+" : unreadCount}
                                    </span>
                            </div>
                            <div className="text-xs md:text-sm lg:text-base">
                                <p className="text-xs lg:text-base">{t("overview-route.unread")}</p>
                                <button 
                                    onClick= {() => navigate("/inbox")}
                                    className="group text-primary-blue font-bold transition hover:text-primary-hover cursor-pointer">
                                        {t("overview-route.inbox")} <span className="inline-block group-hover:animate-arrow-bounce">→</span>
                                </button>
                            </div>
                        </div>
                    )}
                </div>
            }            
        </div>

        {/* Modalen */}
        <CreateAccountModal
            isModalOpen={isCreateAccountOpen}
            onClose={() => setIsCreateAccountOpen(false)}
            onAccountCreated={(account) => {
                setIsCreateAccountOpen(false);
                navigate(`/accounts?account=${account.id}`);
            }}
        />
        </>
    );
}