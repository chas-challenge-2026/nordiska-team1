import { useState } from "react";
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
    SIMULATED_TRANSFER_DELAY_MS,
} from "../components/transfer/transferHelpers";
import {
    OWN_ACCOUNTS,
    BG_PG_PAYEES,
    BANK_PAYEES,
    FAVORITE_ACCOUNT_IDS,
    PLANNED_TRANSFERS,
} from "../constants/transferAccounts";
import type {
    Payee,
    PlannedTransfer,
    TransferAccount,
} from "../constants/transferAccounts";

type Step = "form" | "bankid" | "done";

export default function TransferPage() {
    const { t } = useTranslation();

    const [name, setName] = useState("");
    const [fromId, setFromId] = useState(OWN_ACCOUNTS[0].id);
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
    const [plannedTransfers, setPlannedTransfers] =
        useState<PlannedTransfer[]>(PLANNED_TRANSFERS);

    const allAccounts: TransferAccount[] = [
        ...OWN_ACCOUNTS,
        ...BG_PG_PAYEES,
        ...BANK_PAYEES,
        ...customs,
    ];
    const fromAccount = OWN_ACCOUNTS.find((a) => a.id === fromId) ?? null;
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

        const wrapFrom = (accounts: typeof OWN_ACCOUNTS) =>
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
                    items: wrapFrom(OWN_ACCOUNTS),
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
                    items: wrap(OWN_ACCOUNTS),
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

    const commitTransfer = () => {
        if (!toAccount) return;
        const note = recurring
            ? t("page-transfer.recurring-label")
            : toAccount.own
              ? t("page-transfer.planned.note-internal")
              : t("page-transfer.planned.note-to", { name: toAccount.name });
        const transferName = name.trim() || t("page-transfer.default-name");
        setPlannedTransfers((prev) => [
            { date, name: transferName, note, sum: amountValue },
            ...prev,
        ]);
    };

    const startTransfer = () => {
        setStep("done");
        setTransferPhase("processing");
        const willFail = shouldSimulateFailure(name);
        setTimeout(() => {
            if (willFail) {
                setTransferPhase("failure");
            } else {
                commitTransfer();
                setTransferPhase("success");
            }
        }, SIMULATED_TRANSFER_DELAY_MS);
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
        <div className="min-h-0 min-w-0 flex-1 overflow-y-auto p-10">
            <div className="mx-auto grid max-w-[1240px] grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)] rounded-[10px] border border-[#E5EAF0] bg-white shadow-card">
                <div className="px-10 pt-8 pb-10">
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

                <PlannedTransfersPanel upcomingTransfers={upcomingTransfers} />
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
