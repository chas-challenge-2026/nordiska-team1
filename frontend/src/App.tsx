import './App.css'
import AppRoutes from './routes/AppRoutes'
import { useUserStore } from './store/userStore'
import { useSessionCheck } from './hooks/useSessionCheck'
import PageHeader from './components/PageHeader'

export default function App() {
    const isCheckingSession = useUserStore((state) => state.isCheckingSession);
    useSessionCheck();
    if (isCheckingSession) return <div>loading...</div>
    
    return (
        <>
            <PageHeader />
            <AppRoutes/>
        </>
    )
}
