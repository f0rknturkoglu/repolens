import { Route, Routes } from 'react-router-dom'
import { AppLayout } from '@/components/layout'
import { HomePage } from '@/pages/home'
import { DiscoverPage } from '@/pages/discover'

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route
        path="/discover"
        element={
          <AppLayout>
            <DiscoverPage />
          </AppLayout>
        }
      />
    </Routes>
  )
}
