import { useSyncExternalStore } from "react";

const DESKTOP_QUERY = "(min-width: 768px)"; // Tailwind md (48rem)

function subscribeDesktop(onChange: () => void) {
    const mediaQuery = window.matchMedia(DESKTOP_QUERY);
    mediaQuery.addEventListener("change", onChange);
    return () => mediaQuery.removeEventListener("change", onChange);
}

function getDesktopSnapshot() {
    return window.matchMedia(DESKTOP_QUERY).matches;
}

function getServerSnapshot() {
    return false;
}

export function useIsDesktop() {
    return useSyncExternalStore(subscribeDesktop, getDesktopSnapshot, getServerSnapshot);
}
