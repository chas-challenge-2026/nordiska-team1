import axiosInstance from "./axiosInstance";

export async function downloadTaxReport(accountId: number, accountNumber: string, year?: number): Promise<void> {
    const res = await axiosInstance.get("/reports/tax-report", {
        params: { accountId, year },
        responseType: "blob",
        headers: {
            Accept: "application/pdf",
        },
    });

    const blob = new Blob([res.data], { type: "application/pdf" });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `skatteunderlag_${year ?? new Date().getFullYear()}_${accountNumber}.pdf`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
}

export async function downloadAccountStatement(accountId: number, accountNumber: string): Promise<void> {
    const res = await axiosInstance.get("/reports/statement", {
        params: { accountId },
        responseType: "blob",
        headers: {
            Accept: "application/pdf",
        },
    });

    const blob = new Blob([res.data], { type: "application/pdf" });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `kontoutdrag_${accountNumber}.pdf`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
}

export async function downloadAllDocuments(): Promise<void> {
    const res = await axiosInstance.get("/reports/download-all", {
        responseType: "blob",
        headers: {
            Accept: "application/zip",
        },
    });

    const blob = new Blob([res.data], { type: "application/zip" });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `nordiska_dokument_${new Date().getFullYear()}.zip`;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
}
