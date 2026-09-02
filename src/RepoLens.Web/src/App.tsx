import { Route, Routes } from 'react-router-dom'
import { AppLayout } from '@/components/layout'
import { HomePage } from '@/pages/home'
import { DiscoverPage } from '@/pages/discover'
import { SearchPage } from '@/pages/search'
import { RepositoryDetailPage } from '@/pages/repository-detail'

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
      <Route
        path="/search"
        element={
          <AppLayout>
            <SearchPage />
          </AppLayout>
        }
      />
      <Route
        path="/repositories/:repositoryId"
        element={
          <AppLayout>
            <RepositoryDetailPage />
          </AppLayout>
        }
      />
    </Routes>
  )
}
