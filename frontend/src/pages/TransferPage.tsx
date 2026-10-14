import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import TransferForm from "../components/transfer/TransferForm";
import PlannedTransfersPanel from "../components/transfer/PlannedTransfersPanel";
import PendingTransfersPanel from "../components/transfer/PendingTransfersPanel";
import TransferModals from "../components/transfer/TransferModals";
import TransferDone from "../components/TransferDone";
import type { TransferPhase, TransferSummary } from "../components/TransferDone";
import {
    formatSek,
    parseAmount,
    todayIso,
    shouldSimulateFailure,
    toPlannedDateIso,
    addRepeatIso,
    nextBankDayIso,
    repeatingLabel,
    toOwnAccount,
    SIMULATED_TRANSFER_DELAY_MS,
} from "../components/transfer/transferHelpers";
import type { OwnAccount } from "../constants/transferAccounts";
import { useGetAccounts } from "../hooks/useAccounts";
import { useTransferFunds } from "../hooks/useTransactions";
import { usePlannedTransfers } from "../hooks/usePlannedTransfers";
import { useAccountPicker } from "../hooks/useAccountPicker";
import { useRecurrence } from "../hooks/useRecurrence";
import { usePendingTransfers } from "../hooks/usePendingTransfers";
import TransferCard from "../components/transfer/TransferCard";
import HelpCard from "../components/faq/HelpCard";
import HelpCardBubble from "../components/faq/HelpCardBubble";

type Step = "form" | "bankid" | "done";

export default function TransferPage() {
    const { t } = useTranslation();

    const { data: accountsData } = useGetAccounts("active");
    const transferFundsMutation = useTransferFunds();

    const ownAccounts: OwnAccount[] = useMemo(
        () => (accountsData ?? []).map(toOwnAccount),
        [accountsData],
    );

    const { addLocalPlanned, createPlanned, panelProps } =
        usePlannedTransfers(ownAccounts);
    const { pendingTransfers, addPending } = usePendingTransfers();

    const [name, setName] = useState("");
    const [fromId, setFromId] = useState<string | null>(null);
    const [toId, setToId] = useState<string | null>(null);
    const [amount, setAmount] = useState("");
    const [date, setDate] = useState(todayIso());
    const recurrence = useRecurrence();
    const { recurring, repeating } = recurrence;
    const [step, setStep] = useState<Step>("form");
    const [transferPhase, setTransferPhase] =
        useState<TransferPhase>("processing");

    const { allAccounts, openFromModal, openToModal, closeAll, pickerProps } =
        useAccountPicker({ ownAccounts, fromId, toId, setFromId, setToId });

    const fromAccount = ownAccounts.find((a) => a.id === fromId) ?? null;
    const toAccount = allAccounts.find((a) => a.id === toId) ?? null;

    const amountValue = parseAmount(amount);
    const over = !!fromAccount && amountValue > fromAccount.balance;
    const isExternal = !!toAccount && !toAccount.own;

    const canSubmit =
        !!fromAccount &&
        !!toAccount &&
        amountValue > 0 &&
        !over;

    const ctaHint = canSubmit ? "" : t("page-transfer.cta-hint-incomplete");

    const doneSummary: TransferSummary = {
        amount: formatSek(amountValue),
        fromName: fromAccount?.name ?? "",
        fromMeta: fromAccount?.meta ?? "",
        toName: toAccount?.name ?? "",
        toMeta: toAccount?.meta ?? "",
        date,
        interval: repeating ? repeatingLabel(repeating, t) : undefined,
    };

    const commitLocalPlanned = () => {
        if (!fromAccount || !toAccount) return;
        addLocalPlanned({
            date,
            name: name.trim() || t("page-transfer.default-name"),
            note: repeating ? repeatingLabel(repeating, t) : "",
            repeating,
            sum: amountValue,
            fromName: fromAccount.name,
            toName: toAccount.name,
        });
    };

    const startTransfer = () => {
        if (!fromAccount || !toAccount) return;

        if (!toAccount.own) {
            // BG/PG- och bankmottagare saknar backend-stöd — simulera lokalt som förut.
            setStep("done");
            setTransferPhase("processing");
            const willFail = shouldSimulateFailure(name);
            setTimeout(() => {
                if (willFail) {
                    setTransferPhase("failure");
                } else {
                    // Planerade är bara för framtida eller återkommande
                    // överföringar — en engångsbetalning idag är skickad och
                    // visas som kommande tills den är framme nästa bankdag.
                    if (repeating || date !== todayIso()) {
                        commitLocalPlanned();
                    } else {
                        addPending({
                            name: name.trim() || t("page-transfer.default-name"),
                            fromName: fromAccount.name,
                            toName: toAccount.name,
                            sum: amountValue,
                            arrivesAt: nextBankDayIso(date),
                        });
                    }
                    setTransferPhase("success");
                }
            }, SIMULATED_TRANSFER_DELAY_MS);
            return;
        }

        const label = name.trim() || undefined;
        const isToday = date === todayIso();

        if (!isToday) {
            // Framtida datum (återkommande eller ej): registrera bara en planerad
            // post, exekvera inget nu och visa inget success/failure-steg.
            createPlanned({
                accountId: Number(fromAccount.id),
                type: "Withdraw",
                amount: amountValue,
                plannedDate: toPlannedDateIso(date),
                label,
                targetAccountId: Number(toAccount.id),
                repeating,
            })
                .then(() => handleReset())
                .catch(() => {
                    setStep("done");
                    setTransferPhase("failure");
                });
            return;
        }

        setStep("done");
        setTransferPhase("processing");

        transferFundsMutation
            .mutateAsync({
                sourceAccountId: Number(fromAccount.id),
                targetAccountId: Number(toAccount.id),
                amount: amountValue,
                label,
            })
            .then(() => {
                setTransferPhase("success");
                if (repeating) {
                    // Dagens överföring är redan gjord — lägg nästa
                    // tillfälle i planerade överföringar.
                    createPlanned({
                        accountId: Number(fromAccount.id),
                        type: "Withdraw",
                        amount: amountValue,
                        plannedDate: toPlannedDateIso(addRepeatIso(date, repeating)),
                        label,
                        targetAccountId: Number(toAccount.id),
                        repeating,
                    })
                        .catch(() => {
                            // Dagens överföring lyckades ändå — låt success-sidan stå kvar.
                        });
                }
            })
            .catch(() => setTransferPhase("failure"));
    };

    const handleSubmit = () => {
        if (!canSubmit) return;
        if (isExternal) {
            setStep("bankid");
        } else {
            startTransfer();
        }
    };

    const handleReset = () => {
        setName("");
        setToId(null);
        setAmount("");
        setDate(todayIso());
        recurrence.reset();
        closeAll();
        setStep("form");
        setTransferPhase("processing");
    };

    return (
        <main className="min-h-screen w-full text-dark-navy">
            <div className="mx-auto w-full max-w-7xl px-2 pt-4 pb-20 sm:px-6 sm:pt-8 md:pb-8 lg:px-1 lg:py-10">
                {/* MAIN CONTENT */}
                <div className="grid gap-5 lg:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)] xl:grid-cols-[minmax(0,1.4fr)_minmax(0,1fr)_240px]">
                    {/* TRANSFER FORM */}
                    <TransferCard
                        id="transfer-form-heading"
                        label={t("page-transfer.form-card-label")}
                        title={t("page-transfer.heading")}
                    >
                        {(step === "form" || step === "bankid") && (
                            <TransferForm
                                fromAccount={fromAccount}
                                toAccount={toAccount}
                                onOpenFromModal={openFromModal}
                                onOpenToModal={openToModal}
                                amount={amount}
                                onAmountChange={setAmount}
                                over={over}
                                date={date}
                                onDateChange={setDate}
                                recurring={recurring}
                                onRecurringChange={recurrence.setRecurring}
                                recurrence={recurrence.fieldProps}
                                name={name}
                                onNameChange={setName}
                                isExternal={isExternal}
                                canSubmit={canSubmit}
                                ctaHint={ctaHint}
                                onSubmit={handleSubmit}
                            />
                        )}

                        {step === "done" && (
                            <TransferDone
                                phase={transferPhase}
                                summary={doneSummary}
                                onReset={handleReset}
                            />
                        )}
                    </TransferCard>

                    {/* PLANNED AND PENDING TRANSFERS */}
                    <div className="flex min-w-0 flex-col gap-5">
                        <TransferCard
                            id="planned-transfers-heading"
                            label={t("page-transfer.planned.card-label")}
                            title={t("table.planned-transactions")}
                        >
                            <PlannedTransfersPanel {...panelProps} />
                        </TransferCard>

                        {pendingTransfers.length > 0 && (
                            <TransferCard
                                id="pending-transfers-heading"
                                label={t("page-transfer.pending.card-label")}
                                title={t("table.pending-transfers")}
                            >
                                <PendingTransfersPanel pendingTransfers={pendingTransfers} />
                            </TransferCard>
                        )}
                    </div>

                    {/* HELP / RELATED ARTICLES */}
                    <aside aria-label={t("generic.help")} className="hidden min-w-0 self-start border border-gray-200 bg-white md:shadow-sm xl:block">
                        <div className="min-h-22 items-center justify-between bg-nordiska-blue px-4 py-3 text-white">
                            <p className="mt-2 truncate text-right text-sm uppercase text-white/80">{t("generic.help-center")}</p>
                            <p className="mt-1 truncate text-right text-md font-semibold">{t("generic.related-articles")}</p>
                        </div>

                        <div className="p-4">
                            <HelpCard searchTerms={t("page-transfer.help-card-search")} removeHeading numOfHits={12} />
                        </div>
                    </aside>
                </div>

                <TransferModals
                    {...pickerProps}
                    showBankId={step === "bankid"}
                    fromAccount={fromAccount}
                    toAccount={toAccount}
                    amountValue={amountValue}
                    date={date}
                    onApprove={startTransfer}
                    onCancelBankId={() => setStep("form")}
                />

                {/* HELP BUBBLE */}
                <HelpCardBubble searchTerms={t("page-transfer.help-card-search")} numOfHits={6} />
            </div>
        </main>
    );
}
