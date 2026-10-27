
import { useTranslation } from "react-i18next";
import { formatCurrency } from "../../utils/currency";
import type { SavingGoal } from "../../services/accountsService";
import { useDeleteSavingsGoal } from "../../hooks/useAccounts";
import ActionButton from "./ActionButton";
import PercentagePieChart from "./PercentagePieChart";
import ProgressBar from "./ProgressBar";

type SavingsGoalSectionProps = {
    savingGoal?: SavingGoal;
    accountAmount: number;
    onDeposit: () => void;
    onEdit: (newGoal: boolean) => void;
};

export default function SavingsGoalSection({ savingGoal, accountAmount, onDeposit, onEdit }: SavingsGoalSectionProps) {
    const { t } = useTranslation();
    const deleteSavingsGoalMutation = useDeleteSavingsGoal();

    if (!savingGoal) {
        return (
            <section aria-labelledby="savings-goal-heading">
                <h2 id="savings-goal-heading" className="bg-dark-navy text-base font-medium text-white mb-1 sm:text-lg pl-1">{t("accounts-route.savings-goal-title")}</h2>

                <div className="border border-nordiska-blue/50 bg-light-gray/30 pb-4 flex flex-col items-center justify-center ">
                    <div className="mt-5 flex w-28 items-center justify-center">
                        <span aria-hidden="true" className="block h-11 w-11 bg-nordiska-orange mask-[url('/icons/target.svg')] mask-contain mask-center mask-no-repeat sm:h-15 sm:w-15" />
                    </div>
                    <p className="mt-1 text-center text-sm font-semibold text-dark-navy/80 sm:text-lg">{t("accounts-route.savings-goal.no-goal-title")}</p>
                    <p className="mt-1 px-2 text-center text-xs text-dark-navy/80 sm:px-10 sm:text-base xl:text-sm">
                        {t("accounts-route.savings-goal.no-goal-info")}
                        <span className="sm:block">{" "}{t("accounts-route.savings-goal.no-goal-info-span")}</span>
                    </p>
                    <div className="mt-5">
                        <ActionButton
                            style="regular"
                            onClick={() => onEdit(false)}
                            ariaLabel={t("accounts-route.savings-goal.create-savingsgoal")}
                            prefixIcon="/icons/target.svg"
                            title={t("accounts-route.savings-goal.create-savingsgoal")}
                        />
                    </div>
                </div>
            </section>
        );
    }

    const targetAmount = Math.max(savingGoal.targetAmount, 0);
    const currentAmount = Math.min(Math.max(accountAmount, 0), targetAmount);
    const remainingAmount = Math.max(targetAmount - currentAmount, 0);
    const percentage = targetAmount > 0 ? Math.min(100, Math.max(0, (currentAmount / targetAmount) * 100)) : 0;
    const roundedPercentage = Math.round(percentage);
    const isCompleted = targetAmount > 0 && remainingAmount <= 0;

    const targetDate = savingGoal.targetDate ? new Date(savingGoal.targetDate) : null;
    const isValidTargetDate = targetDate !== null && !Number.isNaN(targetDate.getTime());

    const handleDelete = () => {
        const confirmed = window.confirm(`${t("accounts-route.savings-goal.confirm-delete")} ${savingGoal.title}?`);

        if (!confirmed) return;

        deleteSavingsGoalMutation.mutate(savingGoal.id);
    };

    const getProgressMessage = () => {
        if (roundedPercentage >= 75) return t("accounts-route.savings-goal.progress.almost-there");
        if (roundedPercentage >= 60) return t("accounts-route.savings-goal.progress.great-progress");
        if (roundedPercentage >= 50) return t("accounts-route.savings-goal.progress.halfway");
        if (roundedPercentage >= 40) return t("accounts-route.savings-goal.progress.nearly-halfway");
        if (roundedPercentage >= 25) return t("accounts-route.savings-goal.progress.getting-started");
        if (roundedPercentage >= 10) return t("accounts-route.savings-goal.progress.good-start");
        if (roundedPercentage > 0) return t("accounts-route.savings-goal.progress.lets-go");

        return t("accounts-route.savings-goal.progress.start-saving");
    };

    return (
        <section aria-labelledby="savings-goal-heading" className="bg-white -mt-1">
            <h2 id="savings-goal-heading" className="bg-dark-navy text-base font-medium text-white mb-1 sm:text-lg pl-1 pt-1">{t("accounts-route.savings-goal-title")}</h2>
            <div className="border border-nordiska-blue/50 bg-light-gray/30 p-3 sm:p-4">
            <div className="mb-2 flex flex-col items-start justify-between">
                <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.savings-goal.your-goal")}</p>
                <p className="mb-2 max-w-full break-words text-xl font-bold text-dark-navy sm:text-xl">{savingGoal.title}</p>
            </div>

            <div className="grid gap-5 md:grid-cols-[180px_1fr] md:items-center">
                <div className="relative mx-auto h-40 w-40 max-sm:h-36 max-sm:w-36">
                    <PercentagePieChart percentage={roundedPercentage} />
                </div>

                <div className="min-w-0">
                    <div className="flex items-end justify-between gap-3 border border-nordiska-blue/50 bg-primary-blue/10 p-3 max-sm:flex-col max-sm:items-stretch">
                        {!isCompleted && (
                            <div className="min-w-0">
                                <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.savings-goal.remaining")}</p>
                                <p className="break-words text-xl font-semibold text-dark-navy sm:text-2xl">{formatCurrency(remainingAmount)}</p>
                            </div>
                        )}

                        <div className="min-w-0 max-sm:w-full">
                            {!isCompleted ? (
                                <p className="text-right text-xs uppercase text-nordiska-blue sm:text-sm max-sm:text-left">{t("accounts-route.savings-goal.goal-amount")}</p>
                            ) : (
                                <p className="text-xs font-medium uppercase text-success sm:text-sm">{t("accounts-route.savings-goal.goal-achieved")}</p>
                            )}
                            <p className="break-words text-xl font-bold text-dark-navy sm:text-2xl">{formatCurrency(targetAmount)}</p>
                        </div>
                    </div>

                    <div className="mt-5 flex items-end justify-between border-l-2 border-nordiska-orange py-1 pl-3 max-sm:flex-col max-sm:items-stretch max-sm:gap-3">
                        {!isCompleted ? (
                            <>
                                <div className="w-full min-w-0">
                                    <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.savings-goal.progress-label")}</p>
                                    <p className="mt-1 break-words text-xs leading-5 sm:text-base font-medium">{getProgressMessage()}</p>
                                </div>

                                <button type="button" onClick={onDeposit} className="group inline-flex min-w-0 max-w-[60%] flex-1 cursor-pointer items-center justify-center rounded-lg border bg-white px-2.5 py-1.5 text-xs font-semibold text-primary-blue transition hover:bg-primary-blue hover:text-white sm:w-auto sm:flex-none sm:text-sm max-sm:w-full max-sm:max-w-full max-sm:flex-none">
                                    <span aria-hidden="true" className="mr-1.5 block h-4 w-4 shrink-0 bg-primary-blue mask-[url('/icons/deposit.svg')] mask-contain mask-center mask-no-repeat group-hover:bg-white sm:mr-2 sm:h-5 sm:w-5" />
                                    {t("accounts-route.savings-goal.deposit")}
                                </button>
                            </>
                        ) : (
                            <div className="w-full min-w-0">
                                <p className="text-xs uppercase text-nordiska-blue sm:text-sm">{t("accounts-route.savings-goal.completed-title")}</p>
                                <p className="mt-1 break-words text-xs leading-5 sm:text-base">
                                    {t("accounts-route.savings-goal.completed-info")} <span className="font-medium">"{savingGoal.title}"</span> {t("accounts-route.savings-goal.completed-info-suffix")}
                                </p>
                            </div>
                        )}
                    </div>
                </div>
            </div>

            {!isCompleted && isValidTargetDate && targetDate && (
                <ProgressBar type="time" createdAt={savingGoal.createdAt} targetDate={savingGoal.targetDate} />
            )}

            {!isCompleted ? (
                <div className="mt-6 flex justify-end gap-3 sm:gap-5 max-sm:flex-col max-sm:items-stretch">
                    <ActionButton
                        style="regular"
                        onClick={() => onEdit(false)}
                        ariaLabel={t("accounts-route.savings-goal.edit")}
                        prefixIcon="/icons/target.svg"
                        title={t("accounts-route.savings-goal.edit")}
                    />
                </div>
            ) : (
                <div className="mt-6 flex justify-end gap-3 sm:gap-5 max-sm:flex-col max-sm:items-stretch">
                    <ActionButton
                        style="light"
                        onClick={() => onEdit(true)}
                        ariaLabel={t("accounts-route.savings-goal.create-new")}
                        prefixIcon="/icons/target.svg"
                        title={t("accounts-route.savings-goal.create-new")}
                    />

                    <ActionButton
                        style="cancel"
                        onClick={handleDelete}
                        isPending={deleteSavingsGoalMutation.isPending}
                        disabled={deleteSavingsGoalMutation.isPending}
                        ariaLabel={t("accounts-route.savings-goal.delete")}
                        prefixIcon="/icons/target.svg"
                        title={deleteSavingsGoalMutation.isPending ? t("accounts-route.savings-goal.deleting") : t("accounts-route.savings-goal.delete")}
                    />
                </div>
            )}
            </div>
        </section>
    );
}
