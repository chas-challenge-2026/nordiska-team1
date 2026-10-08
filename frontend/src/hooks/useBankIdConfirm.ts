import { useEffect, useRef, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { bankIdCollect, type BankIdInitRes } from "../services/authService";
import { autoStartUrl } from "../utils/bankId";
import { useBankIdInitate } from "./useLogin";
import { useLogout } from "./useLogout";
import { useUserStore } from "../store/userStore";

export type BankIdMode = "otherDevice" | "thisDevice";

/**
 * Bekräftar en överföring med test-BankID.
 *
 * Backend har ingen sign-endpoint än, så vi återanvänder inloggningens
 * initiate/collect. Vid COMPLETE sätter backend en ny inloggningscookie för
 * den som legitimerade sig — är det någon annan än den inloggade kunden
 * loggas vi ut och ingen överföring görs. Byt till en riktig
 * /bankid/sign-endpoint när backend har en.
 */
export function useBankIdConfirm(onSigned: () => void) {
    const user = useUserStore((state) => state.user);
    const clearUser = useUserStore((state) => state.logout);
    const { mutate: logout } = useLogout();

    const [mode, setMode] = useState<BankIdMode | null>(null);
    const [initData, setInitData] = useState<BankIdInitRes | null>(null);
    const orderRef = initData?.orderRef ?? "";
    const handledOrderRef = useRef<string | null>(null);

    const {
        mutate: initiate,
        error: initError,
        reset: resetInit,
    } = useBankIdInitate();

    // Samma pollning som inloggningen, men utan setUser — vem som
    // signerade kontrolleras nedan innan något annat händer.
    const collect = useQuery({
        queryKey: ["bankIdConfirm", orderRef],
        queryFn: () => bankIdCollect(orderRef),
        enabled: !!orderRef,
        retry: false,
        gcTime: 0,
        refetchInterval: (query) => {
            const status = query.state.data?.status;
            if (status === "COMPLETE" || status === "FAILED") return false;
            if (query.state.error) return false;
            return 1500;
        },
    });

    const status = collect.data?.status;
    const signer = collect.data?.customer;
    const wrongUser = status === "COMPLETE" && signer?.id !== user?.id;

    useEffect(() => {
        if (status !== "COMPLETE" || handledOrderRef.current === orderRef) return;
        handledOrderRef.current = orderRef;

        if (!wrongUser) {
            onSigned();
            return;
        }
        // Sessionen tillhör nu någon annan — logga ut helt.
        logout(undefined, { onSettled: () => clearUser("notAuthorized") });
    }, [status, orderRef, wrongUser, onSigned, logout, clearUser]);

    function start(nextMode: BankIdMode) {
        setMode(nextMode);
        setInitData(null);
        resetInit();

        initiate(undefined, {
            onSuccess: (data) => {
                setInitData(data);
                if (nextMode === "thisDevice") {
                    window.location.href = autoStartUrl(data.autoStartToken);
                }
            },
        });
    }

    return {
        mode,
        initData,
        status,
        hintCode: collect.data?.hintCode?.toLowerCase(),
        initError,
        collectError: collect.error,
        wrongUser,
        start,
    };
}
