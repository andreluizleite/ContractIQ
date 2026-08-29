import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'

import { App } from './App'

const customer = {
  id: '11111111-1111-4111-8111-111111111111',
  name: 'ACME Corporation',
}

const contract = {
  id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
  customerId: customer.id,
  startDate: '2026-01-01',
  status: 'active',
  monthlyFee: { amount: 1200, currency: 'USD' },
}

const contractDetails = {
  ...contract,
  noticePeriodDays: 30,
  minimumCommitmentEndDate: '2028-01-01',
  earlyTerminationPenaltyRate: 0.25,
}

const assessment = {
  contractId: contract.id,
  isAllowed: true,
  reason: 'allowed',
  requestedOn: '2026-08-28',
  earliestTerminationDate: '2026-09-27',
  chargeableMonthlyPeriods: 16,
  penalty: { amount: 4800, currency: 'USD' },
  hasPenalty: true,
}

function response(body: unknown, status = 200) {
  return Promise.resolve({
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as Response)
}

function createApiMock() {
  return vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
    const path = String(input)

    if (path === '/api/v1/customers') {
      return response([customer])
    }

    if (path === `/api/v1/customers/${customer.id}/contracts`) {
      return response([contract])
    }

    if (path === `/api/v1/contracts/${contract.id}/cancellation-assessment`) {
      return response(assessment)
    }

    if (path === `/api/v1/contracts/${contract.id}`) {
      return response(contractDetails)
    }

    if (path === '/api/v1/assistant/answers' && init?.method === 'POST') {
      const requestBody = JSON.parse(String(init.body)) as { question: string }
      const proposesAction = requestBody.question
        .toLowerCase()
        .includes('create')

      return response({
        answer: proposesAction
          ? 'I prepared the cancellation request. Review the preview before confirming [1].'
          : 'ACME can request cancellation. The deterministic penalty applies [1].',
        language: 'en',
        hasSufficientEvidence: true,
        assessment,
        modelId: 'test-chat-model',
        proposedAction: proposesAction
          ? {
              name: 'create_cancellation_request',
              intent: 'create_cancellation_request',
              requiresConfirmation: true,
              canExecute: true,
              assessment,
            }
          : null,
        citations: [
          {
            number: 1,
            documentKey: 'contract-acme',
            title: 'ACME Agreement',
            version: '2.0',
            section: 'Termination for convenience',
            page: 2,
            sourcePath: 'contracts/acme-v2.md',
          },
        ],
      })
    }

    if (
      path === '/api/v1/assistant/actions/cancellation-requests' &&
      init?.method === 'POST'
    ) {
      return response(
        {
          id: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
          contractId: contract.id,
          customerId: customer.id,
          createdAtUtc: '2026-08-28T12:00:00Z',
          requestedOn: assessment.requestedOn,
          earliestTerminationDate: assessment.earliestTerminationDate,
          penalty: assessment.penalty,
          status: 'pendingReview',
        },
        201,
      )
    }

    if (
      path === `/api/v1/contracts/${contract.id}/cancellation-requests` &&
      init?.method === 'POST'
    ) {
      return response(
        {
          id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd',
          contractId: contract.id,
          customerId: customer.id,
          createdAtUtc: '2026-08-28T12:00:00Z',
          requestedOn: assessment.requestedOn,
          earliestTerminationDate: assessment.earliestTerminationDate,
          penalty: assessment.penalty,
          status: 'pendingReview',
        },
        201,
      )
    }

    return response({ detail: 'Unexpected request' }, 404)
  })
}

describe('App', () => {
  beforeEach(() => {
    document.documentElement.lang = 'en'
    vi.stubGlobal('fetch', createApiMock())
  })

  it('shows the English experience and switches to Brazilian Portuguese without a reload', async () => {
    const user = userEvent.setup()
    render(<App />)

    expect(
      screen.getByRole('heading', {
        name: 'Choose a customer',
      }),
    ).toBeInTheDocument()
    expect(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    ).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox'), 'pt-BR')

    expect(
      screen.getByRole('heading', {
        name: 'Selecione um cliente',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /ACME Corporation/ }),
    ).toBeInTheDocument()
    expect(document.documentElement.lang).toBe('pt-BR')
  })

  it('filters the customer workspace by company name', async () => {
    const user = userEvent.setup()
    const globex = {
      id: '22222222-2222-4222-8222-222222222222',
      name: 'Globex Corporation',
    }
    vi.stubGlobal(
      'fetch',
      vi.fn(() => response([customer, globex])),
    )
    render(<App />)

    expect(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /Globex Corporation/ }),
    ).toBeInTheDocument()

    await user.type(screen.getByRole('searchbox'), 'globex')

    expect(
      screen.queryByRole('button', { name: /ACME Corporation/ }),
    ).not.toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /Globex Corporation/ }),
    ).toBeInTheDocument()
  })

  it('uses contextual drawers without losing selection on compact screens', async () => {
    const user = userEvent.setup()
    vi.stubGlobal(
      'matchMedia',
      vi.fn(() => ({
        matches: true,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
      })),
    )
    const { unmount } = render(<App />)

    const navigatorTrigger = screen.getByRole('button', {
      name: 'Open customer and contract navigator',
    })
    await user.click(navigatorTrigger)

    expect(
      screen.getByRole('dialog', { name: 'Primary navigation' }),
    ).toHaveAttribute('aria-modal', 'true')
    await waitFor(() =>
      expect(
        screen.getByRole('button', {
          name: 'Close customer and contract navigator',
        }),
      ).toHaveFocus(),
    )

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await user.keyboard('{Escape}')
    expect(navigatorTrigger).toHaveFocus()
    await screen.findByRole('heading', { name: 'Cancellation is available' })

    const copilotTrigger = screen.getByRole('button', {
      name: 'Open AI copilot',
    })
    await user.click(copilotTrigger)
    expect(
      screen.getByRole('dialog', { name: 'Ask ContractIQ' }),
    ).toHaveAttribute('aria-modal', 'true')
    await user.keyboard('{Escape}')
    expect(copilotTrigger).toHaveFocus()

    unmount()
    vi.unstubAllGlobals()
  })

  it('completes the customer, assessment, confirmation, and success flow', async () => {
    const user = userEvent.setup()
    const fetchMock = createApiMock()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))

    expect(
      await screen.findByRole('heading', { name: 'Cancellation is available' }),
    ).toBeInTheDocument()
    expect(screen.getByText('$4,800.00')).toBeInTheDocument()
    expect(screen.getByText('16 × $1,200.00 × 25%')).toBeInTheDocument()

    await user.click(
      screen.getByRole('button', { name: 'Create cancellation request' }),
    )
    expect(screen.getByRole('dialog')).toHaveTextContent('ACME Corporation')
    expect(screen.getByRole('dialog')).toHaveTextContent('AAAAAAAA')
    expect(screen.getByRole('dialog')).toHaveTextContent(
      'Deterministic assessment',
    )
    expect(screen.getByRole('dialog')).toHaveTextContent(
      'The contract remains active',
    )

    await user.click(screen.getByRole('button', { name: 'Confirm request' }))
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Confirm that you reviewed the assessment',
    )

    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Confirm request' }))

    expect(await screen.findByText('Request created')).toBeInTheDocument()
    expect(screen.getByText('Pending review')).toBeInTheDocument()

    const postCall = fetchMock.mock.calls.find(
      ([, init]) => init?.method === 'POST',
    )
    expect(postCall?.[1]?.headers).toEqual(
      expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
    )
  })

  it('shows an empty state when a customer has no contracts', async () => {
    const user = userEvent.setup()
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL) =>
        String(input).endsWith('/contracts')
          ? response([])
          : response([customer]),
      ),
    )
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )

    expect(
      await screen.findByText('This customer has no contracts.'),
    ).toBeInTheDocument()
  })

  it('exposes selected context and closes the confirmation dialog with Escape', async () => {
    const user = userEvent.setup()
    render(<App />)

    const customerButton = await screen.findByRole('button', {
      name: /ACME Corporation/,
    })
    await user.click(customerButton)
    expect(customerButton).toHaveAttribute('aria-pressed', 'true')

    const contractButton = await screen.findByRole('button', {
      name: /AAAAAAAA/,
    })
    await user.click(contractButton)
    expect(contractButton).toHaveAttribute('aria-pressed', 'true')
    await screen.findByRole('heading', { name: 'Cancellation is available' })

    const createButton = screen.getByRole('button', {
      name: 'Create cancellation request',
    })
    await user.click(createButton)

    expect(screen.getByRole('dialog')).toHaveFocus()
    await user.tab()
    const checkbox = screen.getByRole('checkbox')
    const confirmButton = screen.getByRole('button', {
      name: 'Confirm request',
    })
    expect(checkbox).toHaveFocus()
    const dialog = screen.getByRole('dialog')
    fireEvent.keyDown(dialog, { code: 'Tab', key: 'Tab', shiftKey: true })
    expect(confirmButton).toHaveFocus()
    fireEvent.keyDown(dialog, { code: 'Tab', key: 'Tab' })
    expect(checkbox).toHaveFocus()
    await user.keyboard('{Escape}')

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(createButton).toHaveFocus()
  })

  it('asks a grounded contract question and renders its citation', async () => {
    const user = userEvent.setup()
    const fetchMock = createApiMock()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })

    await user.type(
      screen.getByLabelText('Contract question'),
      'Can ACME cancel now?',
    )
    await user.click(screen.getByRole('button', { name: 'Ask assistant' }))

    expect(await screen.findByText('Grounded answer')).toBeInTheDocument()
    expect(
      screen.getByText('Can ACME cancel now?', {
        selector: '.conversation-message p',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByText(/ACME can request cancellation/),
    ).toBeInTheDocument()
    expect(screen.getByText('ACME Agreement')).toBeInTheDocument()
    expect(
      screen.getByText(/version 2.0 · Termination for convenience · page 2/),
    ).toBeInTheDocument()

    const assistantCall = fetchMock.mock.calls.find(
      ([path]) => String(path) === '/api/v1/assistant/answers',
    )
    expect(JSON.parse(String(assistantCall?.[1]?.body))).toEqual(
      expect.objectContaining({
        contractId: contract.id,
        customerId: customer.id,
        language: 'en',
      }),
    )
  })

  it('uses a suggested question to start an assistant conversation', async () => {
    const user = userEvent.setup()
    const fetchMock = createApiMock()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })

    await user.click(
      screen.getByRole('button', {
        name: 'Can this contract be cancelled now?',
      }),
    )

    expect(screen.getByLabelText('Contract question')).toHaveValue(
      'Can this contract be cancelled now?',
    )
    await user.click(screen.getByRole('button', { name: 'Ask assistant' }))

    expect(await screen.findByText('Grounded answer')).toBeInTheDocument()
  })

  it('clears an in-flight assistant state when the active context changes', async () => {
    const user = userEvent.setup()
    const baseMock = createApiMock()
    let resolveAssistant!: (value: Response) => void
    const fetchMock = vi.fn((input: RequestInfo | URL, init?: RequestInit) => {
      if (String(input) === '/api/v1/assistant/answers') {
        return new Promise<Response>((resolve) => {
          resolveAssistant = resolve
        })
      }

      return baseMock(input, init)
    })
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })

    await user.type(
      screen.getByLabelText('Contract question'),
      'Can ACME cancel now?',
    )
    await user.click(screen.getByRole('button', { name: 'Ask assistant' }))
    expect(screen.getByRole('status')).toHaveTextContent('Reviewing evidence')

    await user.selectOptions(screen.getByRole('combobox'), 'pt-BR')

    expect(screen.queryByText('Analisando evidências…')).not.toBeInTheDocument()
    expect(
      screen.getByRole('heading', {
        name: 'O que você gostaria de entender?',
      }),
    ).toBeInTheDocument()

    resolveAssistant(
      await response({
        answer: 'This obsolete answer must be ignored.',
        language: 'en',
        hasSufficientEvidence: true,
        assessment,
        modelId: 'test-chat-model',
        proposedAction: null,
        citations: [],
      }),
    )
    await Promise.resolve()
    await Promise.resolve()

    expect(
      screen.queryByText('This obsolete answer must be ignored.'),
    ).not.toBeInTheDocument()
  })

  it('keeps deterministic operations available when the assistant is unavailable', async () => {
    const user = userEvent.setup()
    const baseMock = createApiMock()
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
        String(input) === '/api/v1/assistant/answers'
          ? response({ detail: 'AI unavailable' }, 503)
          : baseMock(input, init),
      ),
    )
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })

    await user.type(screen.getByLabelText('Contract question'), 'Explain this')
    await user.click(screen.getByRole('button', { name: 'Ask assistant' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The AI service is temporarily unavailable',
    )
    expect(
      screen.getByRole('button', { name: 'Create cancellation request' }),
    ).toBeEnabled()
    expect(screen.getByText('$4,800.00')).toBeInTheDocument()
  })

  it('shows insufficient evidence without inventing sources', async () => {
    const user = userEvent.setup()
    const baseMock = createApiMock()
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
        String(input) === '/api/v1/assistant/answers'
          ? response({
              answer: 'The available documents do not support this answer.',
              language: 'en',
              hasSufficientEvidence: false,
              assessment,
              modelId: 'test-chat-model',
              proposedAction: null,
              citations: [],
            })
          : baseMock(input, init),
      ),
    )
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })
    await user.type(screen.getByLabelText('Contract question'), 'Unknown term')
    await user.click(screen.getByRole('button', { name: 'Ask assistant' }))

    expect(
      await screen.findByText('Insufficient contract evidence'),
    ).toBeInTheDocument()
    expect(screen.queryByText('Sources')).not.toBeInTheDocument()
  })

  it('does not expose confirmation for an action rejected by domain rules', async () => {
    const user = userEvent.setup()
    const baseMock = createApiMock()
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
        String(input) === '/api/v1/assistant/answers'
          ? response({
              answer: 'The action cannot be executed.',
              language: 'en',
              hasSufficientEvidence: true,
              assessment,
              modelId: 'test-chat-model',
              proposedAction: {
                name: 'create_cancellation_request',
                intent: 'create_cancellation_request',
                requiresConfirmation: true,
                canExecute: false,
                assessment: { ...assessment, isAllowed: false },
              },
              citations: [],
            })
          : baseMock(input, init),
      ),
    )
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })
    await user.type(screen.getByLabelText('Contract question'), 'Prepare it')
    await user.click(screen.getByRole('button', { name: 'Ask assistant' }))

    expect(
      await screen.findByText('Domain rules do not allow this action.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Review and confirm action' }),
    ).not.toBeInTheDocument()
  })

  it('prepares and confirms a cancellation through the assistant tool', async () => {
    const user = userEvent.setup()
    const fetchMock = createApiMock()
    vi.stubGlobal('fetch', fetchMock)
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })

    await user.type(
      screen.getByLabelText('Contract question'),
      'Create the cancellation request.',
    )
    await user.click(screen.getByRole('button', { name: 'Ask assistant' }))

    expect(
      await screen.findByText('Action prepared by the agent'),
    ).toBeInTheDocument()
    expect(screen.getByText(/No state has changed/)).toBeInTheDocument()
    await user.click(
      screen.getByRole('button', { name: 'Review and confirm action' }),
    )

    expect(screen.getByRole('dialog')).toHaveTextContent(
      'only your explicit confirmation can execute the write tool',
    )
    expect(screen.getByRole('dialog')).toHaveTextContent(
      'AI assistant proposal',
    )
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Confirm request' }))

    expect(await screen.findByText('Request created')).toBeInTheDocument()
    const writeToolCall = fetchMock.mock.calls.find(
      ([path]) =>
        String(path) === '/api/v1/assistant/actions/cancellation-requests',
    )
    expect(writeToolCall?.[1]?.headers).toEqual(
      expect.objectContaining({ 'Idempotency-Key': expect.any(String) }),
    )
    expect(JSON.parse(String(writeToolCall?.[1]?.body))).toEqual(
      expect.objectContaining({
        confirmed: true,
        intent: 'create_cancellation_request',
      }),
    )
  })

  it('shows a recoverable error when the API is unavailable', async () => {
    const user = userEvent.setup()
    let attempt = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(() => {
        attempt += 1
        return attempt === 1
          ? Promise.reject(new TypeError('Network error'))
          : response([])
      }),
    )
    render(<App />)

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'The API is unavailable',
    )
    await user.click(screen.getByRole('button', { name: 'Try again' }))

    await waitFor(() =>
      expect(
        screen.getByText('No customers are available.'),
      ).toBeInTheDocument(),
    )
  })

  it('keeps the review open when a duplicate request returns a conflict', async () => {
    const user = userEvent.setup()
    const baseMock = createApiMock()
    vi.stubGlobal(
      'fetch',
      vi.fn((input: RequestInfo | URL, init?: RequestInit) =>
        String(input).endsWith('/cancellation-requests') &&
        init?.method === 'POST'
          ? response({ detail: 'Duplicate request' }, 409)
          : baseMock(input, init),
      ),
    )
    render(<App />)

    await user.click(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    )
    await user.click(await screen.findByRole('button', { name: /AAAAAAAA/ }))
    await screen.findByRole('heading', { name: 'Cancellation is available' })
    await user.click(
      screen.getByRole('button', { name: 'Create cancellation request' }),
    )
    await user.click(screen.getByRole('checkbox'))
    await user.click(screen.getByRole('button', { name: 'Confirm request' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'A cancellation request is already open',
    )
    expect(screen.getByRole('dialog')).toBeInTheDocument()
  })
})
