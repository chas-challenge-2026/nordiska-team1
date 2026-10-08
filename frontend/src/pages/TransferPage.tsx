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
    const [selectedFromId, setFromId] = useState<string | null>(null);
    const [toId, setToId] = useState<string | null>(null);
    const [amount, setAmount] = useState("");
    const [date, setDate] = useState(todayIso());
    const recurrence = useRecurrence();
    const { recurring, repeating } = recurrence;
    const [step, setStep] = useState<Step>("form");
    const [transferPhase, setTransferPhase] =
        useState<TransferPhase>("processing");

    const fromId = selectedFromId ?? ownAccounts[0]?.id ?? null;

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
        !over &&
        recurrence.isValid;

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
        <div className="min-h-0 min-w-0 flex-1 overflow-y-auto p-4 pb-20 sm:p-6 sm:pb-20 md:pb-6 lg:p-10 bg-light-gray">
            <div className="mx-auto grid max-w-[1240px] grid-cols-1 gap-5 lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]">

                <div className="px-4 pt-6 pb-6 sm:px-6 sm:pt-8 sm:pb-8 lg:px-10 lg:pt-8 lg:pb-10 rounded-xl shadow-md border border-secondary bg-white">
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
                </div>

                <div className="flex min-w-0 flex-col gap-5">
                    <div className="px-4 pt-6 pb-6 sm:px-6 sm:pt-8 sm:pb-8 lg:px-10 lg:pt-8 lg:pb-10 rounded-xl shadow-md border border-secondary bg-white">
                        <PlannedTransfersPanel {...panelProps} />
                    </div>

                    {pendingTransfers.length > 0 && (
                        <div className="px-4 pt-6 pb-6 sm:px-6 sm:pt-8 sm:pb-8 lg:px-10 lg:pt-8 lg:pb-10 rounded-xl shadow-md border border-secondary bg-white">
                            <PendingTransfersPanel pendingTransfers={pendingTransfers} />
                        </div>
                    )}
                </div>
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
        </div>
    );
}
