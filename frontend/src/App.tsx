import './App.css'
import PageHeader from './components/PageHeader'
import LandingPage from './pages/LandingPage'
import CollapsiblePlayground from './pages/CollapsiblePlayground'
import { Routes, Route } from 'react-router'

function App() {
    return (
        <>
            <Routes>
                <Route path="/welcome" element={<LandingPage />} />
                <Route path="/inactive" element={<LandingPage inactive />} />
                <Route path="/dev/collapsible" element={<CollapsiblePlayground />} />
            </Routes>
        </>
    )
}

export default App
