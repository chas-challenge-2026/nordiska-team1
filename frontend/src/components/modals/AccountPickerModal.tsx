import { useTranslation } from "react-i18next";

export type AccountPickerItem = {
    id: string;
    name: string;
    meta: string;
    balance?: string;
    selected: boolean;
};

export type AccountPickerGroup = {
    title: string;
    items: AccountPickerItem[];
};

type AccountPickerModalProps = {
    title: string;
    search: string;
    onSearchChange: (value: string) => void;
    groups: AccountPickerGroup[];
    isEmpty: boolean;
    onSelect: (id: string) => void;
    onClose: () => void;
    onAddNew?: () => void;
};

/**
 * Innehållet i kontoväljar-modalen (renderas inuti `Modal`). Listan grupperas
 * av föräldern (t.ex. Favoriter, Mina konton, BG/PG, Bankkonto).
 * "Lägg till nytt konto" visas bara när `onAddNew` skickas in (dvs. inte i
 * "Från"-vyn, där det inte är meningsfullt att lägga till ett externt konto).
 */
export default function AccountPickerModal({
    title,
    search,
    onSearchChange,
    groups,
    isEmpty,
    onSelect,
    onClose,
    onAddNew,
}: AccountPickerModalProps) {
    const { t } = useTranslation();

    return (
        <>
            <div className="border-b border-[#E5EAF0] px-7 py-6 pb-[18px]">
                <div className="flex items-center justify-between gap-4">
                    <h3 className="m-0 text-xl font-semibold text-dark-navy">
                        {title}
                    </h3>
                    <button
                        type="button"
                        onClick={onClose}
                        aria-label={t("generic.close")}
                        className="cursor-pointer border-0 bg-none px-2 py-1 text-2xl leading-none text-secondary"
                    >
                        ×
                    </button>
                </div>
                <input
                    type="text"
                    value={search}
                    onChange={(e) => onSearchChange(e.target.value)}
                    placeholder={t("page-transfer.modal.search-placeholder")}
                    className="mt-4 w-full rounded-md border border-[#7E8996] px-3 py-2.5 text-base text-dark-navy"
                />
            </div>

            <div className="flex-1 overflow-y-auto px-7 pt-2 pb-24">
                {groups.map((group) => (
                    <div key={group.title} className="pt-4.5">
                        <div className="pb-1.5 text-xs font-bold tracking-[0.1em] text-secondary uppercase">
                            {group.title}
                        </div>
                        {group.items.map((item) => (
                            <button
                                key={item.id}
                                type="button"
                                onClick={() => onSelect(item.id)}
                                className="flex w-full cursor-pointer items-start gap-3.5 border-0 border-b border-[#EEF1F4] bg-none px-1 py-3.5 text-left hover:bg-[#F7F9FB]"
                            >
                                <span
                                    className={`mt-0.5 flex h-4.5 w-4.5 flex-none items-center justify-center rounded-full border-2 ${
                                        item.selected
                                            ? "border-nordiska-blue"
                                            : "border-[#7E8996]"
                                    }`}
                                >
                                    <span
                                        className={`h-2 w-2 rounded-full ${
                                            item.selected
                                                ? "bg-nordiska-blue"
                                                : "bg-transparent"
                                        }`}
                                    />
                                </span>
                                <span className="min-w-0 flex-1">
                                    <span className="flex items-baseline justify-between gap-3">
                                        <span className="truncate text-[15px] font-bold text-dark-navy">
                                            {item.name}
                                        </span>
                                        {item.balance && (
                                            <span className="flex-none text-[15px] font-bold text-dark-navy">
                                                {item.balance}
                                            </span>
                                        )}
                                    </span>
                                    <span className="mt-0.5 block text-[13px] text-secondary">
                                        {item.meta}
                                    </span>
                                </span>
                            </button>
                        ))}
                    </div>
                ))}

                {isEmpty && (
                    <p className="my-7 text-sm text-secondary">
                        {t("page-transfer.modal.empty")}
                    </p>
                )}
            </div>

            {onAddNew && (
                <button
                    type="button"
                    onClick={onAddNew}
                    className="absolute right-6 bottom-[22px] cursor-pointer rounded-full border-0 bg-nordiska-blue px-[22px] py-[13px] text-sm font-bold text-white shadow-floating hover:bg-login-bg"
                >
                    {t("page-transfer.modal.add-new")}
                </button>
            )}
        </>
    );
}
