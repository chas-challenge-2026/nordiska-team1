import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { createMemoryRouter, RouterProvider } from 'react-router'
import Admin from '../../../src/pages/admin/Admin'
import { useUserStore } from '../../../src/store/userStore'

const { loginMutate, logoutMutate } = vi.hoisted(() => ({
    loginMutate: vi.fn(),
    logoutMutate: vi.fn(),
}))

vi.mock('../../../src/hooks/useLogin', () => ({
    useLogin: () => ({ mutate: loginMutate, isPending: false }),
}))
vi.mock('../../../src/hooks/useLogout', () => ({
    useLogout: () => ({ mutate: logoutMutate }),
}))

function renderAdmin() {
    const router = createMemoryRouter(
        [
            { path: '/', element: <div>home</div> },
            { path: '/admin', element: <Admin /> },
            { path: '/admin/faq', element: <div>faq page</div> },
        ],
        { initialEntries: ['/admin'] }
    )
    render(<RouterProvider router={router} />)
    return router
}

async function submitLogin() {
    await userEvent.type(screen.getByPlaceholderText('Ange e-post'), 'u@x.se')
    await userEvent.type(screen.getByPlaceholderText('Ange lösenord'), 'pw')
    await userEvent.click(screen.getByRole('button', { name: 'Logga in' }))
}

beforeEach(() => {
    useUserStore.setState(useUserStore.getInitialState(), true)
    vi.resetAllMocks()
})

describe('Admin', () => {
    describe('role routing', () => {
        it('redirects Customer to /', async () => {
            useUserStore.setState({ user: { id: 0, email: 'c@x.se', role: 'Customer' } })
            const router = renderAdmin()
            await waitFor(() => expect(router.state.location.pathname).toBe('/'))
        })

        it('shows login form when no user', () => {
            renderAdmin()
            expect(screen.getByPlaceholderText('Ange e-post')).toBeInTheDocument()
        })

        it('shows login form for non-Admin, non-Customer role', () => {
            useUserStore.setState({ user: { id: 0, email: 's@x.se', role: 'Staff' } })
            renderAdmin()
            expect(screen.getByPlaceholderText('Ange e-post')).toBeInTheDocument()
            expect(screen.queryByText('Hantera FAQ')).not.toBeInTheDocument()
        })

        it('shows panel for Admin', () => {
            useUserStore.setState({ user: { id: 0, email: 'a@x.se', role: 'Admin' } })
            renderAdmin()
            expect(screen.getByText('Hantera FAQ')).toBeInTheDocument()
            expect(screen.queryByPlaceholderText('Ange e-post')).not.toBeInTheDocument()
        })

        it('Admin navigates to FAQ', async () => {
            useUserStore.setState({ user: { id: 0, email: 'a@x.se', role: 'Admin' } })
            const router = renderAdmin()
            await userEvent.click(screen.getByRole('button', { name: 'Hantera FAQ' }))
            expect(router.state.location.pathname).toBe('/admin/faq')
        })
    })

    describe('login', () => {
        it('shows panel after Admin login', async () => {
            loginMutate.mockImplementation((_creds, opts) =>
                opts.onSuccess({ email: 'a@x.se', role: 'Admin' })
            )
            renderAdmin()
            await submitLogin()
            expect(await screen.findByText('Hantera FAQ')).toBeInTheDocument()
        })

        it('blocks Customer login and logs out', async () => {
            loginMutate.mockImplementation((_creds, opts) =>
                opts.onSuccess({ email: 'c@x.se', role: 'Customer' })
            )
            logoutMutate.mockImplementation((_arg, opts) => opts.onSuccess())
            renderAdmin()
            await submitLogin()
            expect(screen.getByText('BEHÖRIGHET SAKNAS FÖR ANVÄNDARE:')).toBeInTheDocument()
            expect(logoutMutate).toHaveBeenCalled()
            expect(useUserStore.getState().user).toBeNull()
        })

        it.each([
            [401, 'Fel e-post eller lösenord.'],
            [423, 'Kontot är tillfälligt spärrat. Försök igen senare.'],
            [429, 'För många försök. Vänta en stund och försök igen.'],
            [500, 'Något gick fel. Försök igen senare.'],
        ])('status %i shows message', async (status, message) => {
            loginMutate.mockImplementation((_creds, opts) =>
                opts.onError({ isAxiosError: true, response: { status } })
            )
            renderAdmin()
            await submitLogin()
            expect(screen.getByText(message)).toBeInTheDocument()
        })
    })

    describe('logout', () => {
        it('clears user', async () => {
            useUserStore.setState({ user: { id: 0, email: 'a@x.se', role: 'Admin' } })
            logoutMutate.mockImplementation((_arg, opts) => opts.onSuccess())
            renderAdmin()
            await userEvent.click(screen.getByRole('button', { name: 'Logga ut' }))
            expect(useUserStore.getState().user).toBeNull()
        })
    })
})
