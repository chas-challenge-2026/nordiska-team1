import './App.css'
import AppRoutes from './routes/AppRoutes'
import { useUserStore } from './store/userStore'
import { useSessionCheck } from './hooks/useSessionCheck'
import PageHeader from './components/header/PageHeader'
import { useLocation } from 'react-router'

export default function App() {
    const location = useLocation();

    const isCheckingSession = useUserStore((state) => state.isCheckingSession);
    useSessionCheck();
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
        </>
    )
}
