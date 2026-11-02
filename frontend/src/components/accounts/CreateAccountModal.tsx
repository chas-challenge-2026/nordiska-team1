import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import Modal from "../modals/Modal";
import InputField from "../forms/InputField";
import { CollapsibleFormBtns } from "../forms/CollapsibleFormButtons";
import { useCreateAccount, useAccountTypes } from "../../hooks/useAccounts";
import type { AccountTypes, Account } from "../../services/accountsService";
import { useUserStore } from "../../store/userStore";
import { buildSavingsForecast } from "../../utils/savingsForecast";
import ForecastResult from "./ForecastResult";
import SliderField from "./SliderField";

type CreateAccountModalProps = {
    onClose: () => void;
    isModalOpen: boolean;
    onAccountCreated: (account: Account) => void;
};

interface AccountTypeOption {
    value: AccountTypes,
    label: string,
    interestRate: number,
}

interface AccountTypeResponse {
    accountType: AccountTypes;
    interestRate: number;
    description: string;
}

const DEFAULT_START_AMOUNT = 10_000;
const DEFAULT_MONTHLY_AMOUNT = 500;
const DEFAULT_YEARS = 5;

const formatRate = (rate: number) => `${(rate * 100).toLocaleString("sv-SE", { maximumFractionDigits: 2 })} %`;
const formatKr = (value: number) => `${value.toLocaleString("sv-SE", { maximumFractionDigits: 2 })} kr`;

export default function CreateAccountModal({ onClose, isModalOpen = false, onAccountCreated }: CreateAccountModalProps) {
    const { t } = useTranslation();
    const createAccount = useCreateAccount();
    const { user } = useUserStore();
    const { data: accountTypes, isLoading, error } = useAccountTypes();

    // Sorterade efter ränta, högst först
    const accountTypeOptions = useMemo<AccountTypeOption[]>(() => {
        if (!accountTypes) return [];
        return accountTypes
            .map((item: AccountTypeResponse) => ({
                value: item.accountType as AccountTypes,
                label: t(`accounts-route.account-type-${item.accountType}`),
                interestRate: item.interestRate,
            }))
            .sort((a: AccountTypeOption, b: AccountTypeOption) => b.interestRate - a.interestRate);
    }, [accountTypes, t]);
    const bestRate = accountTypeOptions[0]?.interestRate;

    const [accountName, setAccountName] = useState("");
    const [accountType, setAccountType] = useState<AccountTypes | "">("");
    const [initialDeposit, setInitialDeposit] = useState("");
    const [accountTypeError, setAccountTypeError] = useState<string>();
    // const [depositError, setDepositError] = useState<string>();

    // Sparexemplet påverkar bara visningen, inget av det skickas till backend
    const [startAmount, setStartAmount] = useState(DEFAULT_START_AMOUNT);
    const [monthlyAmount, setMonthlyAmount] = useState(DEFAULT_MONTHLY_AMOUNT);
    const [years, setYears] = useState(DEFAULT_YEARS);

    // Prognosen visas i en egen vy i samma modal, "Tillbaka" går till Skapa konto igen
    const [view, setView] = useState<"create" | "forecast">("create");
    const headingRef = useRef<HTMLHeadingElement>(null);
    const hasSwitchedView = useRef(false);

    // Flytta fokus till rubriken när vyn byts, så att skärmläsare och tangentbord hänger med
    useEffect(() => {
        if (!hasSwitchedView.current) return;
        headingRef.current?.focus();
    }, [view]);

    function showView(next: "create" | "forecast") {
        hasSwitchedView.current = true;
        setView(next);
    }

    function openForecast() {
        // Utan valt konto visas prognosen för kontot med bäst ränta
        if (!accountType && accountTypeOptions[0]) setAccountType(accountTypeOptions[0].value);
        showView("forecast");
    }

    const selectedRate = accountTypeOptions.find((opt) => opt.value === accountType)?.interestRate;
    const forecast = useMemo(
        () => selectedRate === undefined ? undefined : buildSavingsForecast(startAmount, selectedRate, monthlyAmount, years),
        [selectedRate, startAmount, monthlyAmount, years],
    );

    function resetForm() {
        setAccountName("");
        setAccountType("");
        setInitialDeposit("");
        setAccountTypeError(undefined);
        // setDepositError(undefined);
        setStartAmount(DEFAULT_START_AMOUNT);
        setMonthlyAmount(DEFAULT_MONTHLY_AMOUNT);
        setYears(DEFAULT_YEARS);
        setView("create");
        hasSwitchedView.current = false;
        createAccount.reset();
    }

    function handleSubmit(e: React.SubmitEvent) {
        e.preventDefault();

        let hasError = false;

        if (!accountType) {
            setAccountTypeError(t("accounts-route.account-type-required"));
            hasError = true;
        } else {
            setAccountTypeError(undefined);
        }

        const parsedDeposit = initialDeposit.trim() === "" ? undefined : Number(initialDeposit);
        if (parsedDeposit !== undefined && Number.isNaN(parsedDeposit)) {
            // setDepositError(t("accounts-route.invalid-amount"));
            hasError = true;
        } else {
            // setDepositError(undefined);
        }


        if (hasError || !accountType) return;

        createAccount.mutate(
            {
                customerId: user!.id,
                accountName: accountName.trim() === "" ? null : accountName,
                accountType,
                initialDeposit: parsedDeposit,
            },
            { onSuccess: (newAccount) => onAccountCreated(newAccount) }
        );
    }

    const accountTypeLabel = (
        <span className="text-sm font-bold text-dark-navy">
            {t("accounts-route.account-type")} <span className="font-light text-red-600"> *</span>
        </span>
    );

    return (
        <Modal isOpen={isModalOpen} onClose={onClose} onCloseAnimationComplete={resetForm} title={t("accounts-route.new-account")}>
            {view === "create" ? (
                <form onSubmit={handleSubmit} className="flex flex-col gap-4 p-6">
                    <h2 ref={headingRef} tabIndex={-1} className="text-lg font-semibold text-dark-navy outline-none">{t("accounts-route.create-new-account")}</h2>

                    <InputField
                        name="accountName"
                        type="text"
                        label={t("accounts-route.account-name")}
                        placeholder={t("accounts-route.account-name-placeholder")}
                        value={accountName}
                        onChange={setAccountName}
                    />
                    <span className="text-xs text-right -mt-3">{accountName.length}/40</span>

                    <fieldset aria-describedby={accountTypeError ? "accountType-error" : undefined}>
                        <legend className="w-full">
                            <span className="flex items-center justify-between">
                                {accountTypeLabel}
                                {accountTypeError && (
                                    <span id="accountType-error" className="text-sm text-red-700">
                                        {accountTypeError}
                                    </span>
                                )}
                            </span>
                        </legend>

                        {isLoading || error ? (
                            <p className="mt-1 text-sm text-secondary">
                                {isLoading ? t("accounts-route.loading-account-types") : t("accounts-route.error-account-types")}
                            </p>
                        ) : (
                            <div className="mt-1 flex flex-col gap-2">
                                {accountTypeOptions.map((opt) => {
                                    const isSelected = opt.value === accountType;
                                    return (
                                        <label
                                            key={opt.value}
                                            className={`flex cursor-pointer items-center gap-3 rounded-md border px-3 py-2 transition has-focus-visible:ring-2 has-focus-visible:ring-primary-blue ${isSelected ? "border-primary-blue bg-lightblue" : "border-nordiska-blue/30 bg-white hover:border-primary-blue/60"}`}
                                        >
                                            <input
                                                type="radio"
                                                name="accountType"
                                                value={opt.value}
                                                checked={isSelected}
                                                onChange={() => {
                                                    setAccountType(opt.value);
                                                    setAccountTypeError(undefined);
                                                }}
                                                className="sr-only"
                                            />
                                            <span aria-hidden="true" className={`flex h-4 w-4 shrink-0 items-center justify-center rounded-full border-2 ${isSelected ? "border-primary-blue" : "border-nordiska-blue/50"}`}>
                                                {isSelected && <span className="h-2 w-2 rounded-full bg-primary-blue" />}
                                            </span>
                                            <span className="min-w-0 flex-1">
                                                <span className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                                                    <span className="text-sm font-semibold text-dark-navy sm:text-base">{opt.label}</span>
                                                    {opt.interestRate === bestRate && (
                                                        <span className="rounded-full bg-green-100 px-2 py-0.5 text-[11px] font-semibold text-green-700">{t("accounts-route.best-rate")}</span>
                                                    )}
                                                </span>
                                                <span className="block text-xs text-secondary">{t(`accounts-route.account-type-desc-${opt.value}`)}</span>
                                            </span>
                                            <span className="shrink-0 text-sm font-bold text-dark-navy sm:text-base">{formatRate(opt.interestRate)}</span>
                                        </label>
                                    );
                                })}
                            </div>
                        )}
                    </fieldset>

                    {accountTypeOptions.length > 0 && (
                        <button
                            type="button"
                            onClick={openForecast}
                            className="-mt-2 self-start cursor-pointer text-sm font-semibold text-primary-blue underline-offset-2 hover:underline"
                        >
                            {t("accounts-route.compare-accounts")}
                        </button>
                    )}

                    {/* <InputField
                        name="initialDeposit"
                        type="number"
                        label={t("accounts-route.initial-deposit")}
                        placeholder="0"
                        value={initialDeposit}
                        onChange={setInitialDeposit}
                        error={depositError}
                        suffix="sek"
                    /> */}
                    {createAccount.isError && (
                        <p className="text-sm text-red-700">{t("accounts-route.create-account-error")}</p>
                    )}

                    <CollapsibleFormBtns onClose={onClose} />
                </form>
            ) : (
                <div className="flex flex-col gap-4 p-6">
                    <h2 ref={headingRef} tabIndex={-1} className="text-lg font-semibold text-dark-navy outline-none">{t("accounts-route.forecast-section.heading")}</h2>

                    <div>
                        <label htmlFor="forecastAccountType">{accountTypeLabel}</label>
                        <select
                            id="forecastAccountType"
                            value={accountType}
                            onChange={(e) => {
                                setAccountType(e.target.value as AccountTypes);
                                setAccountTypeError(undefined);
                            }}
                            className="mt-1 w-full rounded-md border border-nordiska-blue bg-white px-3 py-2"
                        >
                            {accountTypeOptions.map((opt) => (
                                <option key={opt.value} value={opt.value}>
                                    {opt.label} - {t("generic.interest")}: {formatRate(opt.interestRate)}
                                    {opt.interestRate === bestRate ? ` (${t("accounts-route.best-rate")})` : ""}
                                </option>
                            ))}
                        </select>
                    </div>

                    {forecast && (
                        <div className="border border-nordiska-blue/50 bg-light-gray/30">
                            <ForecastResult totalInterest={forecast.totalInterest} endBalance={forecast.endBalance} />
                            <div className="flex flex-col gap-4 p-3 sm:p-4">
                                <SliderField
                                    label={t("accounts-route.forecast-section.start-deposit")}
                                    value={startAmount}
                                    min={0}
                                    max={100_000}
                                    step={1_000}
                                    onChange={setStartAmount}
                                    formatValue={formatKr}
                                    unit="kr"
                                    manualEntry="free"
                                    inputMax={10_000_000}
                                />
                                <SliderField
                                    label={t("accounts-route.forecast-section.monthly-saving")}
                                    value={monthlyAmount}
                                    min={0}
                                    max={10_000}
                                    step={100}
                                    onChange={setMonthlyAmount}
                                    formatValue={formatKr}
                                    unit="kr"
                                    manualEntry="free"
                                    inputMax={1_000_000}
                                />
                                <SliderField
                                    label={t("accounts-route.forecast-section.saving-period")}
                                    value={years}
                                    min={1}
                                    max={10}
                                    step={1}
                                    onChange={setYears}
                                    formatValue={(value) => t("accounts-route.forecast-section.years", { count: value })}
                                    unit={t("accounts-route.forecast-section.years-unit")}
                                />
                                <p className="text-[11px] leading-4 text-secondary sm:text-xs sm:leading-5">{t("accounts-route.create-forecast-info")}</p>
                            </div>
                        </div>
                    )}

                    <div className="flex justify-end pt-2">
                        <button
                            type="button"
                            onClick={() => showView("create")}
                            className="cursor-pointer font-semibold uppercase text-primary-blue"
                        >
                            {t("accounts-route.back-to-create")}
                        </button>
                    </div>
                </div>
            )}
        </Modal>
    );
}
