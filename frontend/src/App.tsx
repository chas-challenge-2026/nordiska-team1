import './App.css'
import PageHeader from './components/PageHeader'
import LandingPage from './pages/LandingPage'
import { Routes, Route } from 'react-router'

function App() {
    return (
        <>
            <Routes>
                <Route path="/welcome" element={<LandingPage />} />
                <Route path="/inactive" element={<LandingPage inactive />} />
            </Routes>
        </>
    )
}

export default App
