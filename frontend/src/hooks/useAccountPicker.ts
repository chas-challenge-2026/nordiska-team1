import { useState } from "react";
import { useTranslation } from "react-i18next";
import type { ModalKind } from "../components/transfer/TransferModals";
import type { AccountPickerGroup } from "../components/modals/AccountPickerModal";
import type { NewAccountValues } from "../components/modals/AddAccountForm";
import { formatSek, matchesSearch } from "../components/transfer/transferHelpers";
import type {
    OwnAccount,
    TransferAccount,
} from "../constants/transferAccounts";
import { useExternalAccounts } from "./useExternalAccounts";

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
    const { accounts: customs, addAccount } = useExternalAccounts();

    const allAccounts: TransferAccount[] = [...ownAccounts, ...customs];

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
            const bgAccounts = customs.filter((c) => c.kind === "bg");
            const bankAccounts = customs.filter((c) => c.kind === "bank");
            groups = [
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
        const isBgPg = values.type === "bgpg";
        const account = addAccount({
            kind: isBgPg ? "bg" : "bank",
            name: values.name,
            meta: isBgPg
                ? values.number
                : `${values.clearing}, ${values.number}`,
        });
        setToId(account.id);
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
