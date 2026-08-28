export type Money = {
  amount: number
  currency: string
}

export type CustomerSummary = {
  id: string
  name: string
}

export type ContractStatus = 'active' | 'cancelled' | 'expired'

export type ContractSummary = {
  id: string
  customerId: string
  startDate: string
  status: ContractStatus
  monthlyFee: Money
}

export type ContractDetails = ContractSummary & {
  noticePeriodDays: number
  minimumCommitmentEndDate: string
  earlyTerminationPenaltyRate: number
}

export type AssessmentReason =
  'allowed' | 'contractAlreadyCancelled' | 'contractExpired'

export type CancellationAssessment = {
  contractId: string
  isAllowed: boolean
  reason: AssessmentReason
  requestedOn: string
  earliestTerminationDate: string
  chargeableMonthlyPeriods: number
  penalty: Money
  hasPenalty: boolean
}

export type CancellationRequest = {
  id: string
  contractId: string
  customerId: string
  createdAtUtc: string
  requestedOn: string
  earliestTerminationDate: string
  penalty: Money
  status: 'pendingReview'
}

type ProblemDetails = {
  detail?: string
  code?: string
  traceId?: string
}

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code?: string,
    public readonly traceId?: string,
    detail?: string,
  ) {
    super(detail || 'The request could not be completed.')
    this.name = 'ApiError'
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response

  try {
    response = await fetch(path, {
      ...init,
      headers: {
        Accept: 'application/json',
        ...init?.headers,
      },
    })
  } catch {
    throw new ApiError(0)
  }

  if (!response.ok) {
    let problem: ProblemDetails = {}

    try {
      problem = (await response.json()) as ProblemDetails
    } catch {
      // A proxy or network boundary may return a non-JSON response.
    }

    throw new ApiError(
      response.status,
      problem.code,
      problem.traceId,
      problem.detail,
    )
  }

  return (await response.json()) as T
}

export const contractIqApi = {
  listCustomers(signal?: AbortSignal) {
    return request<CustomerSummary[]>('/api/v1/customers', { signal })
  },

  listCustomerContracts(customerId: string, signal?: AbortSignal) {
    return request<ContractSummary[]>(
      `/api/v1/customers/${customerId}/contracts`,
      {
        signal,
      },
    )
  },

  getContract(contractId: string, signal?: AbortSignal) {
    return request<ContractDetails>(`/api/v1/contracts/${contractId}`, {
      signal,
    })
  },

  assessCancellation(contractId: string, signal?: AbortSignal) {
    return request<CancellationAssessment>(
      `/api/v1/contracts/${contractId}/cancellation-assessment`,
      { signal },
    )
  },

  createCancellationRequest(contractId: string, idempotencyKey: string) {
    return request<CancellationRequest>(
      `/api/v1/contracts/${contractId}/cancellation-requests`,
      {
        method: 'POST',
        headers: {
          'Idempotency-Key': idempotencyKey,
        },
      },
    )
  },
}
