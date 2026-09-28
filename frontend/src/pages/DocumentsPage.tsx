import { useState } from "react";
import { useGetAccounts } from "../hooks/useAccounts";
import { downloadTaxReport, downloadAccountStatement, downloadAllDocuments } from "../services/reportsService";

export default function DocumentsPage() {
    const { data: accounts, isLoading: accountsLoading, isError: accountsError } = useGetAccounts("active");

    const [selectedAccountId, setSelectedAccountId] = useState<number | "">("");
    const [reportType, setReportType] = useState<"tax_report" | "statement">("tax_report");

    const [isDownloadingSingle, setIsDownloadingSingle] = useState(false);
    const [singleError, setSingleError] = useState<string | null>(null);

    const [isDownloadingAll, setIsDownloadingAll] = useState(false);
    const [allError, setAllError] = useState<string | null>(null);

    // Auto-select first account if not yet selected
    const effectiveAccountId = selectedAccountId !== "" ? Number(selectedAccountId) : (accounts?.[0]?.id ?? null);
    const currentAccount = accounts?.find((a) => a.id === effectiveAccountId);

    async function handleDownloadSingle() {
        if (!currentAccount) {
            setSingleError("Vänligen välj ett konto.");
            return;
        }

        try {
            setIsDownloadingSingle(true);
            setSingleError(null);

            if (reportType === "tax_report") {
                await downloadTaxReport(currentAccount.id, currentAccount.accountNumber);
            } else {
                await downloadAccountStatement(currentAccount.id, currentAccount.accountNumber);
            }
        } catch (err) {
            setSingleError(err instanceof Error ? err.message : "Kunde inte ladda ner rapporten.");
        } finally {
            setIsDownloadingSingle(false);
        }
    }

    async function handleDownloadAll() {
        try {
            setIsDownloadingAll(true);
            setAllError(null);
            await downloadAllDocuments();
        } catch (err) {
            setAllError(err instanceof Error ? err.message : "Kunde inte ladda ner arkivet.");
        } finally {
            setIsDownloadingAll(false);
        }
    }

    return (
        <div className="mx-auto w-full max-w-4xl px-4 py-6 sm:px-6 sm:py-10 font-montserrat">
            <div className="mb-8 border-b-2 border-nordiska-orange pb-3">
                <h1 className="text-2xl font-bold text-dark-navy">Dokument &amp; Rapporter</h1>
                <p className="mt-1 text-sm text-secondary">
                    Officiella, digitalt signerade bankunderlag genererade i realtid av den infödda C++-motorn.
                </p>
            </div>

            {accountsLoading && <p className="text-sm text-secondary">Laddar konton...</p>}
            {accountsError && <p className="text-sm text-red-600">Kunde inte hämta konton.</p>}

            {accounts && accounts.length > 0 && (
                <div className="flex flex-col gap-6">
                    {/* SECTION 1: Enskild rapport */}
                    <article className="rounded-lg border border-secondary/15 bg-white p-6 shadow-card">
                        <h2 className="text-lg font-semibold text-dark-navy">Ladda ner enskild rapport</h2>
                        <p className="mt-1 text-sm text-secondary">
                            Välj konto och vilken typ av rapport du vill generera.
                        </p>

                        <div className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-2">
                            {/* Dropdown 1: Konto */}
                            <div className="flex flex-col gap-1.5">
                                <label htmlFor="account-select" className="text-xs font-semibold uppercase tracking-wider text-secondary">
                                    Välj konto
                                </label>
                                <select
                                    id="account-select"
                                    value={effectiveAccountId ?? ""}
                                    onChange={(e) => setSelectedAccountId(Number(e.target.value))}
                                    className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-dark-navy shadow-sm focus:border-nordiska-blue focus:outline-none"
                                >
                                    {accounts.map((acc) => (
                                        <option key={acc.id} value={acc.id}>
                                            {acc.accountName} ({acc.accountNumber})
                                        </option>
                                    ))}
                                </select>
                            </div>

                            {/* Dropdown 2: Rapporttyp */}
                            <div className="flex flex-col gap-1.5">
                                <label htmlFor="report-type-select" className="text-xs font-semibold uppercase tracking-wider text-secondary">
                                    Välj typ av rapport
                                </label>
                                <select
                                    id="report-type-select"
                                    value={reportType}
                                    onChange={(e) => setReportType(e.target.value as "tax_report" | "statement")}
                                    className="rounded-md border border-gray-300 bg-white px-3 py-2 text-sm text-dark-navy shadow-sm focus:border-nordiska-blue focus:outline-none"
                                >
                                    <option value="tax_report">Skatteunderlag / Årsbesked (PDF)</option>
                                    <option value="statement">Kontoutdrag med transaktioner (PDF)</option>
                                </select>
                            </div>
                        </div>

                        <div className="mt-6 flex flex-col items-start gap-2">
                            <button
                                type="button"
                                onClick={handleDownloadSingle}
                                disabled={isDownloadingSingle}
                                className="rounded-md bg-primary-blue px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-nordiska-blue focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-light-blue-accent disabled:opacity-50 cursor-pointer"
                            >
                                {isDownloadingSingle ? "Genererar och laddar ner..." : "Ladda ner rapport"}
                            </button>

                            {singleError && (
                                <p role="alert" className="text-xs text-red-600">
                                    {singleError}
                                </p>
                            )}
                        </div>
                    </article>

                    {/* SECTION 2: Ladda ner alla dokument som arkiv */}
                    <article className="rounded-lg border border-secondary/15 bg-white p-6 shadow-card">
                        <div className="flex flex-col gap-2">
                            <h2 className="text-lg font-semibold text-dark-navy">Ladda ner alla dina dokument som arkiv</h2>
                            <p className="text-sm text-secondary">
                                Samlar alla dina kontoutdrag och skatteunderlag för samtliga aktiva konton i en enda zippad fil.
                            </p>
                        </div>

                        <div className="mt-6 flex flex-col items-start gap-2">
                            <button
                                type="button"
                                onClick={handleDownloadAll}
                                disabled={isDownloadingAll}
                                className="rounded-md bg-dark-navy px-5 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-nordiska-blue focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-light-blue-accent disabled:opacity-50 cursor-pointer"
                            >
                                {isDownloadingAll ? "Genererar arkiv..." : "Ladda ner alla dokument (ZIP)"}
                            </button>

                            {allError && (
                                <p role="alert" className="text-xs text-red-600">
                                    {allError}
                                </p>
                            )}
                        </div>
                    </article>
                </div>
            )}
        </div>
    );
}
