import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import TransferForm from "../components/transfer/TransferForm";
import PlannedTransfersPanel from "../components/transfer/PlannedTransfersPanel";
import TransferModals from "../components/transfer/TransferModals";
import type { ModalKind } from "../components/transfer/TransferModals";
import type { AccountPickerGroup } from "../components/modals/AccountPickerModal";
import type { NewAccountValues } from "../components/modals/AddAccountForm";
import TransferDone from "../components/TransferDone";
import type { TransferPhase } from "../components/TransferDone";
import {
    formatSek,
    parseAmount,
    todayIso,
    matchesSearch,
    shouldSimulateFailure,
    toPlannedDateIso,
    addOneMonthIso,
    SIMULATED_TRANSFER_DELAY_MS,
} from "../components/transfer/transferHelpers";
import {
    BG_PG_PAYEES,
    BANK_PAYEES,
    FAVORITE_ACCOUNT_IDS,
} from "../constants/transferAccounts";
import type {
    OwnAccount,
    Payee,
    PlannedTransfer,
    TransferAccount,
} from "../constants/transferAccounts";
import { useAccounts } from "../hooks/useAccounts";
import {
    useTransactions,
    useTransferFunds,
    useCreatePlannedTransaction,
    useCancelPlannedTransaction,
} from "../hooks/useTransactions";

type Step = "form" | "bankid" | "done";

export default function TransferPage() {
    const { t } = useTranslation();

    const { data: accountsData } = useAccounts();
    const { data: transactionsData } = useTransactions();
    const transferFundsMutation = useTransferFunds();
    const createPlannedMutation = useCreatePlannedTransaction();
    const cancelPlannedMutation = useCancelPlannedTransaction();

    const ownAccounts: OwnAccount[] = useMemo(
        () =>
            (accountsData ?? []).map((a) => ({
                id: String(a.id),
                own: true as const,
                type: a.accountType,
                number: a.accountNumber,
                name: a.accountType,
                meta: a.accountNumber,
                balance: a.balance,
            })),
        [accountsData],
    );

    const [name, setName] = useState("");
    const [fromId, setFromId] = useState<string | null>(null);
    const [toId, setToId] = useState<string | null>(null);
    const [amount, setAmount] = useState("");
    const [date, setDate] = useState(todayIso());
    const [recurring, setRecurring] = useState(false);
    const [modal, setModal] = useState<ModalKind>(null);
    const [search, setSearch] = useState("");
    const [addAccountOpen, setAddAccountOpen] = useState(false);
    const [step, setStep] = useState<Step>("form");
    const [transferPhase, setTransferPhase] =
        useState<TransferPhase>("processing");
    const [customs, setCustoms] = useState<Payee[]>([]);
    const [localPlannedTransfers, setLocalPlannedTransfers] = useState<
        PlannedTransfer[]
    >([]);

    useEffect(() => {
        if (!fromId && ownAccounts.length > 0) {
            setFromId(ownAccounts[0].id);
        }
    }, [fromId, ownAccounts]);

    const backendPlannedTransfers: PlannedTransfer[] = useMemo(
        () =>
            (transactionsData ?? [])
                .filter((tx) => tx.isPlanned)
                .map((tx) => ({
                    localId: `backend-${tx.id}`,
                    source: "backend" as const,
                    backendId: tx.id,
                    date: (tx.plannedDate ?? tx.createdAt).slice(0, 10),
                    name: tx.label?.trim() || t("page-transfer.default-name"),
                    note: tx.repeating ?? "",
                    sum: tx.amount,
                    accountId: tx.accountId,
                    targetAccountId: tx.targetAccountId,
                    type: tx.type,
                    label: tx.label,
                    repeating: tx.repeating,
                })),
        [transactionsData, t],
    );

    const plannedTransfers = [
        ...backendPlannedTransfers,
        ...localPlannedTransfers,
    ];

    const allAccounts: TransferAccount[] = [
        ...ownAccounts,
        ...BG_PG_PAYEES,
        ...BANK_PAYEES,
        ...customs,
    ];
    const fromAccount = ownAccounts.find((a) => a.id === fromId) ?? null;
    const toAccount = allAccounts.find((a) => a.id === toId) ?? null;

    const amountValue = parseAmount(amount);
    const over = !!fromAccount && amountValue > fromAccount.balance;
    const isExternal = !!toAccount && !toAccount.own;

    const canSubmit = !!fromAccount && !!toAccount && amountValue > 0 && !over;

    const ctaHint = canSubmit ? "" : t("page-transfer.cta-hint-incomplete");

    const doneSummary = toAccount
        ? t(
              recurring
                  ? "page-transfer.done.summary-recurring"
                  : "page-transfer.done.summary",
              {
                  amount: formatSek(amountValue),
                  name: toAccount.name,
                  date,
              },
          )
        : "";

    const upcomingTransfers = plannedTransfers.filter(
        (p) => p.date >= todayIso(),
    );

    const query = search.trim().toLowerCase();
    const selectedId = modal === "from" ? fromId : toId;

    const buildGroups = (): AccountPickerGroup[] => {
        const wrap = (accounts: TransferAccount[]) =>
            accounts
                .filter((a) => matchesSearch(a, query))
                .map((a) => ({
                    id: a.id,
                    name: a.name,
                    meta: a.meta,
                    selected: a.id === selectedId,
                }));

        const wrapFrom = (accounts: OwnAccount[]) =>
            accounts
                .filter((a) => matchesSearch(a, query))
                .map((a) => ({
                    id: a.id,
                    name: a.name,
                    meta: a.meta,
                    balance: `${formatSek(a.balance)} sek`,
                    selected: a.id === selectedId,
                }));

        let groups: AccountPickerGroup[] = [];

        if (modal === "from") {
            groups = [
                {
                    title: t("page-transfer.modal.group-own"),
                    items: wrapFrom(ownAccounts),
                },
            ];
        } else if (modal === "to") {
            const favorites = allAccounts.filter((a) =>
                FAVORITE_ACCOUNT_IDS.includes(a.id),
            );
            const bgAccounts = [
                ...BG_PG_PAYEES,
                ...customs.filter((c) => c.kind === "bg"),
            ];
            const bankAccounts = [
                ...BANK_PAYEES,
                ...customs.filter((c) => c.kind === "bank"),
            ];
            groups = [
                {
                    title: t("page-transfer.modal.group-favorites"),
                    items: wrap(favorites),
                },
                {
                    title: t("page-transfer.modal.group-own"),
                    items: wrap(ownAccounts),
                },
                {
                    title: t("page-transfer.modal.group-bg"),
                    items: wrap(bgAccounts),
                },
                {
                    title: t("page-transfer.modal.group-bank"),
                    items: wrap(bankAccounts),
                },
            ];
        }

        return groups.filter((g) => g.items.length > 0);
    };

    const groups = buildGroups();
    const isEmpty = modal !== null && groups.length === 0;

    const handleSelectAccount = (id: string) => {
        if (modal === "from") setFromId(id);
        else if (modal === "to") setToId(id);
        setModal(null);
        setSearch("");
    };

    const handleCloseModal = () => {
        setModal(null);
        setSearch("");
        setAddAccountOpen(false);
    };

    const handleOpenAdd = () => {
        setAddAccountOpen(true);
    };

    const handleSaveAdd = (values: NewAccountValues) => {
        const resolvedType =
            values.type.trim() || t("page-transfer.add-account.default-type");
        const kind = /giro/i.test(resolvedType) ? "bg" : "bank";
        const meta = [
            resolvedType,
            [values.clearing, values.number].filter(Boolean).join(", "),
        ]
            .filter(Boolean)
            .join(" ")
            .trim();
        const id = `custom-${customs.length + 1}`;
        const account: Payee = {
            id,
            own: false,
            kind,
            name:
                values.name.trim() ||
                t("page-transfer.add-account.default-name"),
            meta,
        };
        setCustoms((prev) => [...prev, account]);
        setToId(id);
        handleCloseModal();
    };

    // BG/PG- och bankmottagare saknar backend-stöd (se plan) — de överföringarna
    // simuleras fortfarande lokalt och sparas bara i sidans egen state.
    const commitLocalPlanned = () => {
        if (!toAccount) return;
        const note = recurring
            ? t("page-transfer.recurring-label")
            : t("page-transfer.planned.note-to", { name: toAccount.name });
        const transferName = name.trim() || t("page-transfer.default-name");
        setLocalPlannedTransfers((prev) => [
            {
                localId: `local-${crypto.randomUUID()}`,
                source: "local",
                date,
                name: transferName,
                note,
                sum: amountValue,
            },
            ...prev,
        ]);
    };

    const handleEditPlannedTransfer = (
        transfer: PlannedTransfer,
        newDate: string,
    ) => {
        if (transfer.source === "local") {
            setLocalPlannedTransfers((prev) =>
                prev.map((p) =>
                    p.localId === transfer.localId
                        ? { ...p, date: newDate }
                        : p,
                ),
            );
            return;
        }

        if (transfer.backendId === undefined || transfer.accountId === undefined) {
            return;
        }

        cancelPlannedMutation
            .mutateAsync(transfer.backendId)
            .then(() =>
                createPlannedMutation.mutateAsync({
                    accountId: transfer.accountId!,
                    type: (transfer.type as "Deposit" | "Withdraw") ?? "Withdraw",
                    amount: transfer.sum,
                    plannedDate: toPlannedDateIso(newDate),
                    label: transfer.label,
                    targetAccountId: transfer.targetAccountId,
                    repeating: transfer.repeating,
                }),
            );
    };

    const handleDeletePlannedTransfer = (transfer: PlannedTransfer) => {
        if (transfer.source === "local") {
            setLocalPlannedTransfers((prev) =>
                prev.filter((p) => p.localId !== transfer.localId),
            );
            return;
        }

        if (transfer.backendId === undefined) return;
        cancelPlannedMutation.mutateAsync(transfer.backendId);
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
                    commitLocalPlanned();
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
            createPlannedMutation
                .mutateAsync({
                    accountId: Number(fromAccount.id),
                    type: "Withdraw",
                    amount: amountValue,
                    plannedDate: toPlannedDateIso(date),
                    label,
                    targetAccountId: Number(toAccount.id),
                    repeating: recurring ? "month" : undefined,
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
                if (recurring) {
                    // Dagens överföring är redan gjord — lägg nästa månads
                    // tillfälle i planerade överföringar.
                    createPlannedMutation
                        .mutateAsync({
                            accountId: Number(fromAccount.id),
                            type: "Withdraw",
                            amount: amountValue,
                            plannedDate: toPlannedDateIso(addOneMonthIso(date)),
                            label,
                            targetAccountId: Number(toAccount.id),
                            repeating: "month",
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
        setRecurring(false);
        setModal(null);
        setSearch("");
        setAddAccountOpen(false);
        setStep("form");
        setTransferPhase("processing");
    };

    return (
        <div className="min-h-0 min-w-0 flex-1 overflow-y-auto p-4 sm:p-6 lg:p-10">
            <div className="mx-auto grid max-w-[1240px] grid-cols-1 rounded-[10px] border border-[#E5EAF0] bg-white shadow-card lg:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]">
                <div className="px-4 pt-6 pb-6 sm:px-6 sm:pt-8 sm:pb-8 lg:px-10 lg:pt-8 lg:pb-10">
                    {(step === "form" || step === "bankid") && (
                        <TransferForm
                            fromAccount={fromAccount}
                            toAccount={toAccount}
                            onOpenFromModal={() => {
                                setModal("from");
                                setSearch("");
                            }}
                            onOpenToModal={() => {
                                setModal("to");
                                setSearch("");
                            }}
                            amount={amount}
                            onAmountChange={setAmount}
                            over={over}
                            date={date}
                            onDateChange={setDate}
                            recurring={recurring}
                            onRecurringChange={setRecurring}
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
                            summaryLine={doneSummary}
                            fromName={fromAccount ? fromAccount.name : ""}
                            toName={toAccount ? toAccount.name : ""}
                            onReset={handleReset}
                        />
                    )}
                </div>

                <PlannedTransfersPanel
                    upcomingTransfers={upcomingTransfers}
                    onEditTransfer={handleEditPlannedTransfer}
                    onDeleteTransfer={handleDeletePlannedTransfer}
                />
            </div>

            <TransferModals
                modal={modal}
                addAccountOpen={addAccountOpen}
                search={search}
                onSearchChange={setSearch}
                groups={groups}
                isEmpty={isEmpty}
                onSelectAccount={handleSelectAccount}
                onCloseModal={handleCloseModal}
                onOpenAdd={handleOpenAdd}
                onCancelAdd={() => setAddAccountOpen(false)}
                onSaveAdd={handleSaveAdd}
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
