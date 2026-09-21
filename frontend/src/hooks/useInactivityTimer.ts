import { useState, useEffect, useRef, useCallback } from "react";
import { useUserStore } from "../store/userStore";
import { useLogout } from "./useLogout";
import { useTranslation } from "react-i18next";

const INACTIVITY_TIME = 3 * 60 * 1000; // 3 min
const WARNING_TIME = 2 * 60 * 1000; // 2 min


const WARNING_SECONDS = WARNING_TIME / 1000;

export function useInactivityTimer() {
    const {t} = useTranslation();
    const user = useUserStore((state) => state.user);
    const clearUser = useUserStore((state) => state.logout);

    const { mutate: logout } = useLogout();

    const [showWarning, setShowWarning] = useState(false);
    const showWarningRef = useRef(false);
    const [remainingSeconds, setRemainingSeconds] = useState(WARNING_SECONDS);

    const inactivityTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const warningTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
    const countdownTimerRef = useRef<ReturnType<typeof setInterval> | null>(null);


    const tabTitleRef = useRef(document.title);
    const tabTitleIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);


    // ---------- TAB-TITLE ALERTS ----------
    // Start
    const startTabTitleAlert = useCallback(() => {
        if (tabTitleIntervalRef.current) return;

        const originalTabTitle = tabTitleRef.current;

        tabTitleIntervalRef.current = setInterval(() => {
            document.title =
                document.title === originalTabTitle ? t("inactivity-warning.doc-title") : originalTabTitle;
        }, 1000);
    }, [t]);

    // Stop
    const stopTabTitleAlert = useCallback(() => {
        if (tabTitleIntervalRef.current) {
            clearInterval(tabTitleIntervalRef.current);
            tabTitleIntervalRef.current = null;
        }

        document.title = tabTitleRef.current;
    }, []);
    
    // ---------- TIMERS ----------
    // Clear
    const clearTimers = useCallback(() => {
        if (inactivityTimerRef.current) {clearTimeout(inactivityTimerRef.current);}
        if (warningTimerRef.current) {clearTimeout(warningTimerRef.current);}
        if (countdownTimerRef.current) {clearInterval(countdownTimerRef.current);}
    }, []);

    // Start
    const startTimer = useCallback(() => {
        inactivityTimerRef.current = setTimeout(() => {
            setShowWarning(true);
            showWarningRef.current = true;
            setRemainingSeconds(WARNING_SECONDS);

            startTabTitleAlert();

            countdownTimerRef.current = setInterval(() => {
                setRemainingSeconds((seconds) => {
                    if (seconds <= 1) {
                        if (countdownTimerRef.current) {
                            clearInterval(countdownTimerRef.current);
                        }
                        return 0;
                    }
                    return seconds - 1;
                });
            }, 1000);

            warningTimerRef.current = setTimeout(() => {
                stopTabTitleAlert();
                setShowWarning(false);
                showWarningRef.current = false;

                logout(undefined, {
                    onSuccess: () => {
                        clearUser("inactivity");
                    },
                });
            }, WARNING_TIME);
        }, INACTIVITY_TIME);
    }, [logout, clearUser, startTabTitleAlert, stopTabTitleAlert]);

    // Reset
    const resetTimer = useCallback(() => {
        clearTimers();
        stopTabTitleAlert();

        setShowWarning(false);
        showWarningRef.current = false;
        setRemainingSeconds(WARNING_SECONDS);

        startTimer();
    }, [clearTimers, startTimer, stopTabTitleAlert]);


    // ---------- AKTIVITETSKOLL ----------
    useEffect(() => {
        if (!user) return;

        const handleActivity = () => {
            if (showWarningRef.current) return;

            resetTimer();
        };

        const events = ["mousemove", "mousedown", "keydown", "scroll", "touchstart", "click", ];
        events.forEach((event) => {window.addEventListener(event, handleActivity);});

        startTimer();

        return () => {
            clearTimers();
            events.forEach((event) => {
                window.removeEventListener(event, handleActivity);
            });
        };

    }, [user, resetTimer, startTimer, clearTimers]);

    // ---------- ANVÄNDAR RESPONS ----------
    // Stanna kvar
    const stayLoggedIn = () => {
        resetTimer();
    };

    // Logga ut
    const logoutNow = () => {
        clearTimers();
        stopTabTitleAlert();

        setShowWarning(false);
        showWarningRef.current = false;

        logout(undefined, {
            onSuccess: () => {
                clearUser("manual");
            },
        });
    };

    return {
        showWarning,
        remainingSeconds,
        stayLoggedIn,
        logoutNow,
    };
}