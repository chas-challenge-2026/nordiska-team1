import AppRoutes from './routes/AppRoutes'
import { useUserStore } from './store/userStore'
import { useSessionCheck } from './hooks/useSessionCheck'
import PageHeader from './components/header/PageHeader'
import ErrorBoundary from './components/ErrorBoundary'
import { useLocation } from 'react-router'

import { useSessionActivity } from './hooks/useSessionActivity'
import InactivityWarning from './components/InactivityWarning'
import ServiceAlertBanner from './components/ServiceAlertBanner'
import OperationalMessageLoader from './components/admin/drift/OperationalMessageLoader'

export default function App() {
    const location = useLocation();

    const isCheckingSession = useUserStore((state) => state.isCheckingSession);
    useSessionCheck();

    // TLLFÄLLIG AVSTÄNING AV INAKTIVITETSKOLL -> Kommentera ut:
    const { showWarning, remainingSeconds, stayLoggedIn, logoutNow } = useSessionActivity();

    if (isCheckingSession) return <div>loading...</div>

    const UNPROTECTED_HEADER = [
        "/welcome",
        "/inactive",
        "/login",
        "/logout",
        "/logged-out",
        "/register",
        "/error-500",
    ]

    const protectedHeader = !UNPROTECTED_HEADER.includes(location.pathname);
    const noShow = location.pathname.startsWith("/admin")
    const margin = UNPROTECTED_HEADER.includes(location.pathname);
    const hidden = location.pathname.startsWith("/admin")

    return (
        <div className="flex h-dvh flex-col overflow-scroll">
            <PageHeader protectedHeader={protectedHeader} noShow={noShow}/>
            <OperationalMessageLoader />
            <ServiceAlertBanner margin={margin} hidden={hidden}/>

            <div className="flex min-h-0 flex-1 flex-col">
                <ErrorBoundary key={location.pathname.split("/")[1]}>
                    <AppRoutes />
                </ErrorBoundary>
            </div>
            {/* TLLFÄLLIG AVSTÄNING AV INAKTIVITETSKOLL -> Kommentera ut: */}
            {showWarning && (
                <InactivityWarning
                    remainingSeconds={remainingSeconds}
                    onStayLoggedIn={stayLoggedIn}
                    onLogout={logoutNow} />
            )}
        </div>
    )
}
