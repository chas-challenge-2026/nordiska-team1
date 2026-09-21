import './App.css'
import AppRoutes from './routes/AppRoutes'
import { useUserStore } from './store/userStore'
import { useSessionCheck } from './hooks/useSessionCheck'
import PageHeader from './components/header/PageHeader'
import { useLocation } from 'react-router'
import { useInactivityTimer } from './hooks/useInactivityTimer'
import InactivityWarning from './components/InactivityWarning'

export default function App() {
    const location = useLocation();

    const isCheckingSession = useUserStore((state) => state.isCheckingSession);
    useSessionCheck();

    const {showWarning, remainingSeconds, stayLoggedIn, logoutNow} = useInactivityTimer();

    if (isCheckingSession) return <div>loading...</div>

    const UNPROTECTED_HEADER = [
        "/welcome",
        "/inactive",
        "/login",
        "/logout",
        "/logged-out",
    ]

    const protectedHeader = !UNPROTECTED_HEADER.includes(location.pathname);
    
    return (
        <>
            <PageHeader protectedHeader={protectedHeader}/>
            <AppRoutes/>
            {showWarning && (
                <InactivityWarning 
                    remainingSeconds={remainingSeconds}
                    onStayLoggedIn={stayLoggedIn}
                    onLogout={logoutNow}/>
            )}
        </>
    )
}
