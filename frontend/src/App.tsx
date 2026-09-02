import './App.css'
import LandingPage from './pages/LandingPage'
import CollapsiblePlayground from './pages/CollapsiblePlayground'
import DesktopLayout from './layouts/DesktopLayout'
import OverviewPage from './pages/OverviewPage'
import { Routes, Route } from 'react-router'

function App() {
    return (
        <>
            <Routes>
                <Route path="/welcome" element={<LandingPage />} />
                <Route path="/inactive" element={<LandingPage inactive />} />
                <Route path="/dev/collapsible" element={<CollapsiblePlayground />} />
                <Route path="/" element={<DesktopLayout />}>
                    <Route index element={<OverviewPage />} />
                </Route>
            </Routes>
        </>
    )
}

export default App
