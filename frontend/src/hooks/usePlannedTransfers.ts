import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import type { OwnAccount, PlannedTransfer } from "../constants/transferAccounts";
import type { EditPlannedError } from "../components/transfer/PlannedTransferActionsModal";
import {
    todayIso,
    toPlannedDateIso,
    repeatingLabel,
} from "../components/transfer/transferHelpers";
import {
    useTransactions,
    useCreatePlannedTransaction,
    useCancelPlannedTransaction,
} from "./useTransactions";

type LocalPlannedInput = Pick<
    PlannedTransfer,
    "date" | "name" | "note" | "sum" | "fromName" | "toName" | "repeating"
>;

export type PlannedTransferChanges = {
    date: string;
    /** undefined = inte återkommande. */
    repeating: string | undefined;
};

export function usePlannedTransfers(ownAccounts: OwnAccount[]) {
    const { t } = useTranslation();

    // Stopgap: backend cannot filter isPlanned yet, planned transfers beyond first page are missed
    const { data: transactionsData } = useTransactions({ Page: 1, PageSize: 100, AccountIds: [] });
    const createPlannedMutation = useCreatePlannedTransaction();
    const cancelPlannedMutation = useCancelPlannedTransaction();

    const [localPlannedTransfers, setLocalPlannedTransfers] = useState<PlannedTransfer[]>([]);

    const backendPlannedTransfers: PlannedTransfer[] = useMemo(() => {
        const accountName = (id: number | undefined) =>
            id === undefined
                ? undefined
                : (ownAccounts.find((a) => a.id === String(id))?.name ??
                  t("page-transfer.planned.unknown-account"));

        return (transactionsData?.items ?? [])
            .filter((tx) => tx.isPlanned)
            .map((tx) => ({
                localId: `backend-${tx.id}`,
                source: "backend" as const,
                backendId: tx.id,
                date: (tx.plannedDate ?? tx.createdAt).slice(0, 10),
                name: tx.label?.trim() || t("page-transfer.default-name"),
                note: tx.repeating ? repeatingLabel(tx.repeating, t) : "",
                sum: tx.amount,
                fromName: accountName(tx.accountId),
                toName: accountName(tx.targetAccountId),
                accountId: tx.accountId,
                targetAccountId: tx.targetAccountId,
                type: tx.type,
                label: tx.label,
                repeating: tx.repeating,
            }));
    }, [transactionsData, ownAccounts, t]);

    const plannedTransfers = [
        ...backendPlannedTransfers,
        ...localPlannedTransfers,
    ];

    const upcomingTransfers = plannedTransfers.filter(
        (p) => p.date >= todayIso(),
    );

    // BG/PG- och bankmottagare saknar backend-stöd (se plan) — de överföringarna
    // simuleras fortfarande lokalt och sparas bara i sidans egen state.
    const addLocalPlanned = (transfer: LocalPlannedInput) => {
        setLocalPlannedTransfers((prev) => [
            {
                localId: `local-${crypto.randomUUID()}`,
                source: "local",
                ...transfer,
            },
            ...prev,
        ]);
    };

    const handleEditPlannedTransfer = (
        transfer: PlannedTransfer,
        { date: newDate, repeating }: PlannedTransferChanges,
        onSaved: () => void,
    ) => {
        if (transfer.source === "local") {
            setLocalPlannedTransfers((prev) =>
                prev.map((p) =>
                    p.localId === transfer.localId
                        ? {
                            ...p,
                            date: newDate,
                            repeating,
                            note: repeating ? repeatingLabel(repeating, t) : "",
                        }
                        : p,
                ),
            );
            onSaved();
            return;
        }

        const { backendId, accountId } = transfer;
        if (backendId === undefined || accountId === undefined) {
            return;
        }

        // Backend saknar uppdatering, så ändring = skapa ny + ta bort gammal.
        // Skapa först: misslyckas något blir det i värsta fall en dubblett,
        // aldrig en försvunnen överföring.
        createPlannedMutation.mutate(
            {
                accountId,
                // Transaction type is lowercase; create endpoint expects "Deposit" | "Withdraw"
                type: transfer.type === "deposit" ? "Deposit" : "Withdraw",
                amount: transfer.sum,
                plannedDate: toPlannedDateIso(newDate),
                label: transfer.label,
                targetAccountId: transfer.targetAccountId,
                repeating,
            },
            {
                onSuccess: () =>
                    cancelPlannedMutation.mutate(backendId, {
                        onSuccess: onSaved,
                    }),
            },
        );
    };

    const resetPlannedMutations = () => {
        createPlannedMutation.reset();
        cancelPlannedMutation.reset();
    };

    // "cleanup" = nya överföringen skapades men den gamla kunde inte tas bort.
    const editPlannedError: EditPlannedError = createPlannedMutation.isError
        ? "save"
        : cancelPlannedMutation.isError
            ? "cleanup"
            : null;

    const handleDeletePlannedTransfer = (
        transfer: PlannedTransfer,
        onDeleted: () => void,
    ) => {
        // Lokala överföringar tas bort direkt och går inte via mutationen,
        // så de påverkar aldrig isPending/isError.
        if (transfer.source === "local") {
            setLocalPlannedTransfers((prev) =>
                prev.filter((p) => p.localId !== transfer.localId),
            );
            onDeleted();
            return;
        }

        if (transfer.backendId === undefined) return;
        cancelPlannedMutation.mutate(transfer.backendId, {
            onSuccess: onDeleted,
        });
    };

    return {
        addLocalPlanned,
        createPlanned: createPlannedMutation.mutateAsync,
        panelProps: {
            upcomingTransfers,
            onEditTransfer: handleEditPlannedTransfer,
            onDeleteTransfer: handleDeletePlannedTransfer,
            isSaving:
                createPlannedMutation.isPending ||
                cancelPlannedMutation.isPending,
            editError: editPlannedError,
            isDeleting: cancelPlannedMutation.isPending,
            deleteError: cancelPlannedMutation.isError,
            onResetStatus: resetPlannedMutations,
        },
    };
}
