import { useState } from "react";
import { useTranslation } from "react-i18next";
import Table from "../components/Table";
import TableRow from "../components/TableRow";
import InputField from "../components/InputField";
import AccountPickerModal from "../components/AccountPickerModal";
import type { AccountPickerGroup } from "../components/AccountPickerModal";
import AddAccountForm from "../components/AddAccountForm";
import type { NewAccountValues } from "../components/AddAccountForm";
import BankIdConfirm from "../components/BankIdConfirm";
import TransferDone from "../components/TransferDone";
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

type ModalKind = "from" | "to" | null;
type Step = "form" | "add" | "bankid" | "done";

function formatSek(amount: number) {
    return amount.toLocaleString("sv-SE", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2,
    });
}

function parseAmount(raw: string) {
    const parsed = parseFloat(raw.replace(/\s/g, "").replace(",", "."));
    return Number.isFinite(parsed) ? parsed : 0;
}

function todayIso() {
    return new Date().toISOString().slice(0, 10);
}

function matchesSearch(account: TransferAccount, query: string) {
    if (!query) return true;
    return `${account.name} ${account.meta}`.toLowerCase().includes(query);
}

type AccountTriggerButtonProps = {
    label: string;
    name: string;
    meta: string;
    onClick: () => void;
};

function AccountTriggerButton({
    label,
    name,
    meta,
    onClick,
}: AccountTriggerButtonProps) {
    const { t } = useTranslation();

    return (
        <div>
            <label className="mb-1.5 block text-[13px] font-bold text-dark-navy">
                {label}
            </label>
            <button
                type="button"
                onClick={onClick}
                className="flex min-h-11 w-full cursor-pointer items-center justify-between gap-3 rounded-md border border-nordiska-blue bg-white px-3.5 py-2.5 text-left"
            >
                <span className="min-w-0">
                    <span className="block truncate text-[15px] font-semibold text-dark-navy">
                        {name}
                    </span>
                    <span className="block truncate text-xs text-meta">
                        {meta}
                    </span>
                </span>
                <span className="text-xs font-bold whitespace-nowrap text-primary-blue uppercase tracking-[0.08em]">
                    {t("page-transfer.select")}
                </span>
            </button>
        </div>
    );
}

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
    const [step, setStep] = useState<Step>("form");
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
    const afterBalance = fromAccount ? fromAccount.balance - amountValue : 0;
    const isExternal = !!toAccount && !toAccount.own;

    const goalOn =
        !!toAccount && toAccount.own && !!toAccount.goal && amountValue > 0;
    const goalPercent =
        goalOn && toAccount?.own && toAccount.goal
            ? Math.min(
                  100,
                  Math.round(
                      ((toAccount.balance + amountValue) / toAccount.goal) *
                          100,
                  ),
              )
            : 0;

    const canSubmit =
        !!name.trim() &&
        !!fromAccount &&
        !!toAccount &&
        amountValue > 0 &&
        !over;

    const ctaHint = canSubmit
        ? isExternal
            ? t("page-transfer.cta-hint-external")
            : t("page-transfer.cta-hint-internal")
        : t("page-transfer.cta-hint-incomplete");

    const doneSummary = toAccount
        ? t(
              recurring
                  ? "transfer.done.summary-recurring"
                  : "transfer.done.summary",
              {
                  amount: formatSek(amountValue),
                  name: toAccount.name,
                  date,
              },
          )
        : "";

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

        let groups: AccountPickerGroup[] = [];

        if (modal === "from") {
            groups = [
                {
                    title: t("page-transfer.modal.group-own"),
                    items: wrap(OWN_ACCOUNTS),
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

    const handleOpenAdd = () => {
        setModal(null);
        setStep("add");
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
        setStep("form");
    };

    const commitTransfer = () => {
        if (!toAccount) return;
        const note = recurring
            ? t("page-transfer.recurring-label")
            : toAccount.own
              ? t("page-transfer.planned.note-internal")
              : t("page-transfer.planned.note-to", { name: toAccount.name });
        setPlannedTransfers((prev) => [
            { date, name, note, sum: amountValue },
            ...prev,
        ]);
    };

    const handleSubmit = () => {
        if (!canSubmit) return;
        if (isExternal) {
            setStep("bankid");
        } else {
            commitTransfer();
            setStep("done");
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
        setStep("form");
    };

    return (
        <div className="min-h-0 min-w-0 flex-1 overflow-y-auto p-10">
            <div className="mx-auto grid max-w-[1240px] grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)] rounded-[10px] border border-border-light bg-white shadow-card">
                <div className="px-10 pt-8 pb-10">
                    {step === "form" && (
                        <div>
                            <div className="flex items-end justify-between border-b-[3px] border-nordiska-orange pb-2.5">
                                <h2 className="m-0 text-[26px] font-semibold text-dark-navy">
                                    {t("page-transfer.heading")}
                                </h2>
                            </div>
                            <p className="mt-3.5 mb-6.5 max-w-[44ch] text-sm text-secondary">
                                {t("page-transfer.help-text")}
                            </p>

                            <div className="flex flex-col gap-5.5">
                                <InputField
                                    name="transferName"
                                    type="text"
                                    label={t("page-transfer.name-label")}
                                    placeholder={t(
                                        "page-transfer.name-placeholder",
                                    )}
                                    value={name}
                                    onChange={setName}
                                />

                                <div className="grid grid-cols-2 gap-5">
                                    <AccountTriggerButton
                                        label={t("page-transfer.from-label")}
                                        name={
                                            fromAccount
                                                ? fromAccount.name
                                                : t(
                                                      "page-transfer.select-account-placeholder",
                                                  )
                                        }
                                        meta={
                                            fromAccount
                                                ? fromAccount.meta
                                                : t(
                                                      "page-transfer.from-meta-placeholder",
                                                  )
                                        }
                                        onClick={() => {
                                            setModal("from");
                                            setSearch("");
                                        }}
                                    />
                                    <div>
                                        <AccountTriggerButton
                                            label={t("page-transfer.to-label")}
                                            name={
                                                toAccount
                                                    ? toAccount.name
                                                    : t(
                                                          "page-transfer.select-recipient-placeholder",
                                                      )
                                            }
                                            meta={
                                                toAccount
                                                    ? toAccount.meta
                                                    : t(
                                                          "page-transfer.to-meta-placeholder",
                                                      )
                                            }
                                            onClick={() => {
                                                setModal("to");
                                                setSearch("");
                                            }}
                                        />
                                        {toAccount && (
                                            <div className="mt-2 flex items-center gap-2">
                                                <span
                                                    className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-[3px] text-[11px] font-bold tracking-[0.08em] uppercase ${
                                                        isExternal
                                                            ? "bg-pill-external-bg text-pill-external-fg"
                                                            : "bg-pill-internal-bg text-nordiska-blue"
                                                    }`}
                                                >
                                                    <span
                                                        className={`h-1.5 w-1.5 rounded-full ${
                                                            isExternal
                                                                ? "bg-pill-external-fg"
                                                                : "bg-nordiska-blue"
                                                        }`}
                                                    />
                                                    {isExternal
                                                        ? t(
                                                              "page-transfer.kind-external",
                                                          )
                                                        : t(
                                                              "page-transfer.kind-internal",
                                                          )}
                                                </span>
                                                <span className="text-xs text-meta">
                                                    {isExternal
                                                        ? t(
                                                              "page-transfer.kind-external-help",
                                                          )
                                                        : t(
                                                              "page-transfer.kind-internal-help",
                                                          )}
                                                </span>
                                            </div>
                                        )}
                                    </div>
                                </div>

                                <div>
                                    <InputField
                                        name="transferAmount"
                                        type="text"
                                        label={t("page-transfer.amount-label")}
                                        placeholder={t(
                                            "page-transfer.amount-placeholder",
                                        )}
                                        value={amount}
                                        onChange={(value) =>
                                            setAmount(
                                                value.replace(/[^\d ,]/g, ""),
                                            )
                                        }
                                        suffix={t(
                                            "page-transfer.amount-suffix",
                                        )}
                                        error={
                                            over
                                                ? t(
                                                      "page-transfer.amount-error",
                                                      {
                                                          account:
                                                              fromAccount?.name ??
                                                              "",
                                                      },
                                                  )
                                                : undefined
                                        }
                                    />
                                    <p className="mt-2 text-sm text-secondary">
                                        {t("page-transfer.balance-after")}{" "}
                                        <strong className="text-dark-navy">
                                            {formatSek(afterBalance)} sek
                                        </strong>
                                    </p>
                                    {goalOn && (
                                        <p className="mt-1.5 text-sm text-secondary">
                                            {t("page-transfer.goal-progress", {
                                                percent: goalPercent,
                                            })}
                                        </p>
                                    )}
                                    <div className="mt-3.5 border-l-2 border-border-light pl-3 text-xs text-muted">
                                        {t("page-transfer.reserved-notice")}
                                    </div>
                                </div>

                                <div className="grid grid-cols-2 items-end gap-5">
                                    <InputField
                                        name="transferDate"
                                        type="date"
                                        label={t("page-transfer.date-label")}
                                        placeholder=""
                                        value={date}
                                        onChange={setDate}
                                    />
                                    <label className="flex cursor-pointer items-center gap-2.5 pb-2.5 text-sm text-dark-navy">
                                        <input
                                            type="checkbox"
                                            checked={recurring}
                                            onChange={(e) =>
                                                setRecurring(e.target.checked)
                                            }
                                            className="h-4.5 w-4.5 cursor-pointer accent-[var(--color-nordiska-blue)]"
                                        />
                                        {t("page-transfer.recurring-label")}
                                    </label>
                                </div>

                                <div className="flex items-center gap-4.5 pt-1.5">
                                    <button
                                        type="button"
                                        onClick={handleSubmit}
                                        disabled={!canSubmit}
                                        className={`cursor-pointer rounded-md border-0 bg-nordiska-blue px-7.5 py-3.5 text-[15px] font-bold text-white hover:bg-login-bg disabled:cursor-not-allowed ${
                                            canSubmit
                                                ? "opacity-100"
                                                : "opacity-[0.45]"
                                        }`}
                                    >
                                        {t("page-transfer.cta-submit")}
                                    </button>
                                    <span className="text-sm text-meta">
                                        {ctaHint}
                                    </span>
                                </div>
                            </div>
                        </div>
                    )}

                    {step === "add" && (
                        <AddAccountForm
                            onCancel={() => setStep("form")}
                            onSave={handleSaveAdd}
                        />
                    )}

                    {step === "done" && (
                        <TransferDone
                            summaryLine={doneSummary}
                            fromName={fromAccount ? fromAccount.name : ""}
                            toName={toAccount ? toAccount.name : ""}
                            onReset={handleReset}
                        />
                    )}
                </div>

                <div className="border-l border-border-light px-10 pt-8 pb-10">
                    <Table tableType="planned" handleClick={() => {}}>
                        {plannedTransfers.map((planned, index) => (
                            <TableRow
                                key={`${planned.date}-${index}`}
                                id={`${planned.date}-${index}`}
                                rowType="planned"
                                plannedDate={planned.date}
                                plannedName={planned.name}
                                plannedNote={planned.note}
                                plannedSum={planned.sum}
                            />
                        ))}
                    </Table>
                    <p className="mt-5.5 max-w-[42ch] text-xs text-muted">
                        {t("page-transfer.planned.footnote")}
                    </p>
                </div>
            </div>

            {modal !== null && (
                <AccountPickerModal
                    title={
                        modal === "from"
                            ? t("page-transfer.modal.from-title")
                            : t("page-transfer.modal.to-title")
                    }
                    search={search}
                    onSearchChange={setSearch}
                    groups={groups}
                    isEmpty={isEmpty}
                    onSelect={handleSelectAccount}
                    onClose={() => {
                        setModal(null);
                        setSearch("");
                    }}
                    onAddNew={handleOpenAdd}
                />
            )}

            {step === "bankid" && toAccount && (
                <BankIdConfirm
                    amountFormatted={formatSek(amountValue)}
                    toName={toAccount.name}
                    toMeta={toAccount.meta}
                    fromName={fromAccount ? fromAccount.name : ""}
                    date={date}
                    onApprove={() => {
                        commitTransfer();
                        setStep("done");
                    }}
                    onCancel={() => setStep("form")}
                />
            )}
        </div>
    );
}
