import { render, screen, waitFor } from '@testing-library/react'
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
      return response({
        answer:
          'ACME can request cancellation. The deterministic penalty applies [1].',
        language: 'en',
        hasSufficientEvidence: true,
        assessment,
        modelId: 'test-chat-model',
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
        name: 'Make contract decisions with confidence.',
      }),
    ).toBeInTheDocument()
    expect(
      await screen.findByRole('button', { name: /ACME Corporation/ }),
    ).toBeInTheDocument()

    await user.selectOptions(screen.getByRole('combobox'), 'pt-BR')

    expect(
      screen.getByRole('heading', {
        name: 'Decida sobre contratos com confiança.',
      }),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: /ACME Corporation/ }),
    ).toBeInTheDocument()
    expect(document.documentElement.lang).toBe('pt-BR')
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

    await user.click(
      screen.getByRole('button', { name: 'Create cancellation request' }),
    )
    expect(screen.getByRole('dialog')).toBeInTheDocument()

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
    expect(screen.getByText(/ACME can request cancellation/)).toBeInTheDocument()
    expect(screen.getByText('ACME Agreement')).toBeInTheDocument()
    expect(
      screen.getByText(/version 2.0 · Termination for convenience · page 2/),
    ).toBeInTheDocument()

    const assistantCall = fetchMock.mock.calls.find(
      ([path]) => String(path) === '/api/v1/assistant/answers',
    )
    expect(JSON.parse(String(assistantCall?.[1]?.body))).toEqual(
      expect.objectContaining({ language: 'en' }),
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
})
