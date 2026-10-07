import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { ModalKind } from "../components/transfer/TransferModals";
import type { AccountPickerGroup } from "../components/modals/AccountPickerModal";
import type { NewAccountValues } from "../components/modals/AddAccountForm";
import { formatSek, matchesSearch } from "../components/transfer/transferHelpers";
import {
    BG_PG_PAYEES,
    BANK_PAYEES,
    FAVORITE_ACCOUNT_IDS,
} from "../constants/transferAccounts";
import type {
    OwnAccount,
    Payee,
    TransferAccount,
} from "../constants/transferAccounts";

type UseAccountPickerArgs = {
    ownAccounts: OwnAccount[];
    fromId: string | null;
    toId: string | null;
    setFromId: (id: string) => void;
    setToId: (id: string) => void;
};

export function useAccountPicker({
    ownAccounts,
    fromId,
    toId,
    setFromId,
    setToId,
}: UseAccountPickerArgs) {
    const { t } = useTranslation();

    const [modal, setModal] = useState<ModalKind>(null);
    const [search, setSearch] = useState("");
    const [addAccountOpen, setAddAccountOpen] = useState(false);
    const [customs, setCustoms] = useState<Payee[]>([]);

    const allAccounts: TransferAccount[] = [
        ...ownAccounts,
        ...BG_PG_PAYEES,
        ...BANK_PAYEES,
        ...customs,
    ];

    const query = search.trim().toLowerCase();
    const selectedId = modal === "from" ? fromId : toId;
    // Kontot som är valt på andra sidan går inte att välja (samma från och till).
    const otherSideId = modal === "from" ? toId : fromId;
    const otherSideReason =
        modal === "from"
            ? t("page-transfer.modal.selected-as-to")
            : t("page-transfer.modal.selected-as-from");

    const buildGroups = (): AccountPickerGroup[] => {
        // Saldo visas bara för egna konton, aldrig för externa mottagare.
        const wrap = (accounts: TransferAccount[]) =>
            accounts
                .filter((a) => matchesSearch(a, query))
                .map((a) => ({
                    id: a.id,
                    name: a.name,
                    meta: a.meta,
                    balance: a.own ? `${formatSek(a.balance)} sek` : undefined,
                    selected: a.id === selectedId,
                    disabledReason:
                        a.id === otherSideId ? otherSideReason : undefined,
                }));

        let groups: AccountPickerGroup[] = [];

        if (modal === "from") {
            groups = [
                {
                    title: t("page-transfer.modal.group-own"),
                    items: wrap(ownAccounts),
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

    const openModal = (kind: "from" | "to") => {
        setModal(kind);
        setSearch("");
    };

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

    return {
        allAccounts,
        openFromModal: () => openModal("from"),
        openToModal: () => openModal("to"),
        closeAll: handleCloseModal,
        pickerProps: {
            modal,
            addAccountOpen,
            search,
            onSearchChange: setSearch,
            groups,
            isEmpty,
            onSelectAccount: handleSelectAccount,
            onCloseModal: handleCloseModal,
            onOpenAdd: handleOpenAdd,
            onCancelAdd: () => setAddAccountOpen(false),
            onSaveAdd: handleSaveAdd,
        },
    };
}
