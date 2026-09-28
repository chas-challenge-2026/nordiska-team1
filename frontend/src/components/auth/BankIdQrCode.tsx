import { useEffect, useState } from "react";
import { QRCodeSVG } from "qrcode.react";

interface BankIdQrCodeProps {
    qrStartToken: string;
    qrStartSecret: string;
    autoStartToken?: string;
    status?: string;
    onCancel: () => void;
}

export default function BankIdQrCode({
    qrStartToken,
    qrStartSecret,
    autoStartToken,
    onCancel
}: BankIdQrCodeProps) {
    const [qrValue, setQrValue] = useState<string>("");

    useEffect(() => {
        let isMounted = true;
        const startTime = Date.now();

        async function updateQr(elapsed: number) {
            try {
                const encoder = new TextEncoder();
                const keyData = encoder.encode(qrStartSecret);
                const msgData = encoder.encode(elapsed.toString());

                const cryptoKey = await window.crypto.subtle.importKey(
                    "raw",
                    keyData,
                    { name: "HMAC", hash: "SHA-256" },
                    false,
                    ["sign"]
                );

                const signature = await window.crypto.subtle.sign("HMAC", cryptoKey, msgData);
                const hashArray = Array.from(new Uint8Array(signature));
                const qrAuthCode = hashArray.map((b) => b.toString(16).padStart(2, "0")).join("");

                if (isMounted) {
                    setQrValue(`bankid.${qrStartToken}.${elapsed}.${qrAuthCode}`);
                }
            } catch (err) {
                console.error("Failed to generate BankID QR code:", err);
            }
        }

        updateQr(0);

        const interval = setInterval(() => {
            const elapsed = Math.floor((Date.now() - startTime) / 1000);
            updateQr(elapsed);
        }, 1000);

        return () => {
            isMounted = false;
            clearInterval(interval);
        };
    }, [qrStartToken, qrStartSecret]);

    return (
        <div className="flex flex-col items-center gap-3 py-3 text-center">
            {qrValue ? (
                <div className="rounded-lg bg-white p-2 shadow-sm border border-gray-200">
                    <QRCodeSVG value={qrValue} size={160} level="M" />
                </div>
            ) : (
                <div className="flex h-[160px] w-[160px] items-center justify-center rounded-lg bg-gray-100 text-xs text-gray-400">
                    Laddar QR-kod...
                </div>
            )}

            <p className="text-xs font-medium text-gray-700">
                Öppna BankID-appen och scanna QR-koden
            </p>

            {autoStartToken && (
                <a
                    href={`bankid:///?autostarttoken=${autoStartToken}&redirect=null`}
                    className="rounded bg-nordiska-orange px-3 py-1.5 text-xs font-semibold text-white transition hover:opacity-90"
                >
                    Öppna BankID på denna enhet
                </a>
            )}

            <button
                type="button"
                onClick={onCancel}
                className="text-xs text-gray-500 hover:text-red-600 underline cursor-pointer"
            >
                Avbryt
            </button>
        </div>
    );
}
