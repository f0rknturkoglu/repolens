import { Route, Routes } from 'react-router-dom'
import { AppLayout } from '@/components/layout'
import { HomePage } from '@/pages/home'
import { DiscoverPage } from '@/pages/discover'
import { SearchPage } from '@/pages/search'
import { EcosystemPage } from '@/pages/ecosystem'
import { IdeaValidationPage } from '@/pages/idea-validation'
import { PortfolioPage } from '@/pages/portfolio'
import { RecommendationPage } from '@/pages/recommendations'
import { AccountPage } from '@/pages/account'
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
        path="/ecosystem"
        element={
          <AppLayout>
            <EcosystemPage />
          </AppLayout>
        }
      />
      <Route
        path="/validate"
        element={
          <AppLayout>
            <IdeaValidationPage />
          </AppLayout>
        }
      />
      <Route
        path="/portfolio"
        element={
          <AppLayout>
            <PortfolioPage />
          </AppLayout>
        }
      />
      <Route
        path="/account"
        element={
          <AppLayout>
            <AccountPage />
          </AppLayout>
        }
      />
      <Route
        path="/recommend"
        element={
          <AppLayout>
            <RecommendationPage />
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
