import { useEffect, useMemo, useState } from 'react'

import {
  ApiError,
  contractIqApi,
  type CancellationAssessment,
  type CancellationRequest,
  type ContractAnswer,
  type ContractDetails,
  type ContractSummary,
  type CustomerSummary,
  type Money,
} from './api'
import { translations, type Language } from './translations'

type Loadable<T> =
  | { status: 'loading' }
  | { status: 'error'; error: unknown }
  | { status: 'ready'; data: T }

type ContractWorkspace = {
  details: ContractDetails
  assessment: CancellationAssessment
}

const initialLoad = { status: 'loading' } as const

function formatDate(value: string, language: Language) {
  return new Intl.DateTimeFormat(language, {
    dateStyle: 'medium',
    timeZone: 'UTC',
  }).format(new Date(`${value}T00:00:00Z`))
}

function formatMoney(money: Money, language: Language) {
  return new Intl.NumberFormat(language, {
    style: 'currency',
    currency: money.currency,
  }).format(money.amount)
}

function shortId(id: string) {
  return id.slice(0, 8).toUpperCase()
}

function errorMessage(error: unknown, language: Language) {
  const copy = translations[language]

  if (error instanceof ApiError) {
    if (error.status === 409) {
      return copy.conflict
    }

    return error.status === 0 ? copy.apiUnavailable : copy.requestFailed
  }

  return copy.apiUnavailable
}

export function App() {
  const [language, setLanguage] = useState<Language>('en')
  const [customers, setCustomers] =
    useState<Loadable<CustomerSummary[]>>(initialLoad)
  const [customersReload, setCustomersReload] = useState(0)
  const [selectedCustomerId, setSelectedCustomerId] = useState<string>()
  const [contracts, setContracts] = useState<Loadable<
    ContractSummary[]
  > | null>(null)
  const [contractsReload, setContractsReload] = useState(0)
  const [selectedContractId, setSelectedContractId] = useState<string>()
  const [workspace, setWorkspace] =
    useState<Loadable<ContractWorkspace> | null>(null)
  const [workspaceReload, setWorkspaceReload] = useState(0)
  const [confirmationOpen, setConfirmationOpen] = useState(false)
  const [assessmentConfirmed, setAssessmentConfirmed] = useState(false)
  const [confirmationError, setConfirmationError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)
  const [createdRequest, setCreatedRequest] = useState<CancellationRequest>()
  const [idempotencyKey, setIdempotencyKey] = useState<string>()
  const [assistantQuestion, setAssistantQuestion] = useState('')
  const [assistantAnswer, setAssistantAnswer] = useState<ContractAnswer>()
  const [assistantError, setAssistantError] = useState<string>()
  const [askingAssistant, setAskingAssistant] = useState(false)
  const copy = translations[language]

  const selectedCustomer = useMemo(
    () =>
      customers.status === 'ready'
        ? customers.data.find((customer) => customer.id === selectedCustomerId)
        : undefined,
    [customers, selectedCustomerId],
  )

  useEffect(() => {
    const controller = new AbortController()

    contractIqApi
      .listCustomers(controller.signal)
      .then((data) => setCustomers({ status: 'ready', data }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setCustomers({ status: 'error', error })
        }
      })

    return () => controller.abort()
  }, [customersReload])

  useEffect(() => {
    if (!selectedCustomerId) {
      return
    }

    const controller = new AbortController()

    contractIqApi
      .listCustomerContracts(selectedCustomerId, controller.signal)
      .then((data) => setContracts({ status: 'ready', data }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setContracts({ status: 'error', error })
        }
      })

    return () => controller.abort()
  }, [selectedCustomerId, contractsReload])

  useEffect(() => {
    if (!selectedContractId) {
      return
    }

    const controller = new AbortController()

    Promise.all([
      contractIqApi.getContract(selectedContractId, controller.signal),
      contractIqApi.assessCancellation(selectedContractId, controller.signal),
    ])
      .then(([details, assessment]) =>
        setWorkspace({ status: 'ready', data: { details, assessment } }),
      )
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setWorkspace({ status: 'error', error })
        }
      })

    return () => controller.abort()
  }, [selectedContractId, workspaceReload])

  function changeLanguage(nextLanguage: Language) {
    setLanguage(nextLanguage)
    setAssistantAnswer(undefined)
    setAssistantError(undefined)
    document.documentElement.lang = nextLanguage
  }

  function selectCustomer(customerId: string) {
    setSelectedCustomerId(customerId)
    setContracts(initialLoad)
    setSelectedContractId(undefined)
    setWorkspace(null)
    setCreatedRequest(undefined)
    resetAssistant()
  }

  function selectContract(contractId: string) {
    setSelectedContractId(contractId)
    setWorkspace(initialLoad)
    setCreatedRequest(undefined)
    resetAssistant()
  }

  function retryCustomers() {
    setCustomers(initialLoad)
    setCustomersReload((value) => value + 1)
  }

  function retryContracts() {
    setContracts(initialLoad)
    setContractsReload((value) => value + 1)
  }

  function retryWorkspace() {
    setWorkspace(initialLoad)
    setWorkspaceReload((value) => value + 1)
  }

  function openConfirmation() {
    setAssessmentConfirmed(false)
    setConfirmationError(undefined)
    setIdempotencyKey(globalThis.crypto.randomUUID())
    setConfirmationOpen(true)
  }

  function closeConfirmation() {
    if (!submitting) {
      setConfirmationOpen(false)
    }
  }

  async function confirmCancellation() {
    if (!assessmentConfirmed) {
      setConfirmationError(copy.confirmationRequired)
      return
    }

    if (!selectedContractId || !idempotencyKey) {
      return
    }

    setSubmitting(true)
    setConfirmationError(undefined)

    try {
      const request = await contractIqApi.createCancellationRequest(
        selectedContractId,
        idempotencyKey,
      )
      setCreatedRequest(request)
      setConfirmationOpen(false)
    } catch (error) {
      setConfirmationError(errorMessage(error, language))
    } finally {
      setSubmitting(false)
    }
  }

  function resetAssistant() {
    setAssistantQuestion('')
    setAssistantAnswer(undefined)
    setAssistantError(undefined)
  }

  async function askAssistant() {
    if (!selectedCustomerId || !selectedContractId || assistantQuestion.trim().length < 3) {
      return
    }

    setAskingAssistant(true)
    setAssistantAnswer(undefined)
    setAssistantError(undefined)

    try {
      const answer = await contractIqApi.askContractQuestion(
        assistantQuestion.trim(),
        selectedCustomerId,
        selectedContractId,
        language,
      )
      setAssistantAnswer(answer)
    } catch (error) {
      setAssistantError(
        error instanceof ApiError && error.status === 503
          ? copy.assistantUnavailable
          : errorMessage(error, language),
      )
    } finally {
      setAskingAssistant(false)
    }
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#workspace" aria-label="ContractIQ home">
          <span className="brand-mark" aria-hidden="true">
            CQ
          </span>
          <span>ContractIQ</span>
        </a>

        <label className="language-picker">
          <span>{copy.languageLabel}</span>
          <select
            aria-label={copy.languageLabel}
            value={language}
            onChange={(event) => changeLanguage(event.target.value as Language)}
          >
            <option value="en">English</option>
            <option value="pt-BR">Português (Brasil)</option>
          </select>
        </label>
      </header>

      <main id="workspace">
        <section className="page-intro" aria-labelledby="page-title">
          <p className="eyebrow">{copy.productEyebrow}</p>
          <h1 id="page-title">{copy.title}</h1>
          <p>{copy.description}</p>
        </section>

        <section className="operations-layout" aria-label={copy.productEyebrow}>
          <aside className="customer-panel">
            <div className="panel-heading">
              <p className="step-number">01</p>
              <div>
                <h2>{copy.customers}</h2>
                <p>{copy.customersDescription}</p>
              </div>
            </div>

            {customers.status === 'loading' && (
              <p className="state-message" role="status">
                {copy.loadingCustomers}
              </p>
            )}

            {customers.status === 'error' && (
              <ErrorState
                message={errorMessage(customers.error, language)}
                retryLabel={copy.retry}
                onRetry={retryCustomers}
              />
            )}

            {customers.status === 'ready' && customers.data.length === 0 && (
              <p className="state-message">{copy.noCustomers}</p>
            )}

            {customers.status === 'ready' && customers.data.length > 0 && (
              <div className="customer-list">
                {customers.data.map((customer) => (
                  <button
                    className="customer-button"
                    data-selected={customer.id === selectedCustomerId}
                    key={customer.id}
                    onClick={() => selectCustomer(customer.id)}
                    type="button"
                  >
                    <span className="customer-avatar" aria-hidden="true">
                      {customer.name.slice(0, 2).toUpperCase()}
                    </span>
                    <span>
                      <strong>{customer.name}</strong>
                      <small>{shortId(customer.id)}</small>
                    </span>
                    <span className="chevron" aria-hidden="true">
                      →
                    </span>
                  </button>
                ))}
              </div>
            )}
          </aside>

          <div className="contract-panel">
            {!selectedCustomer && (
              <EmptyState
                title={copy.chooseCustomer}
                description={copy.chooseCustomerDescription}
              />
            )}

            {selectedCustomer && (
              <>
                <div className="contract-panel-header">
                  <div>
                    <p className="step-number">02</p>
                    <h2>{selectedCustomer.name}</h2>
                  </div>
                  <span>{copy.contracts}</span>
                </div>

                {contracts?.status === 'loading' && (
                  <p className="state-message" role="status">
                    {copy.loadingContracts}
                  </p>
                )}

                {contracts?.status === 'error' && (
                  <ErrorState
                    message={errorMessage(contracts.error, language)}
                    retryLabel={copy.retry}
                    onRetry={retryContracts}
                  />
                )}

                {contracts?.status === 'ready' &&
                  contracts.data.length === 0 && (
                    <EmptyState title={copy.noContracts} />
                  )}

                {contracts?.status === 'ready' && contracts.data.length > 0 && (
                  <>
                    <div className="contract-tabs" aria-label={copy.contracts}>
                      {contracts.data.map((contract) => (
                        <button
                          type="button"
                          key={contract.id}
                          data-selected={contract.id === selectedContractId}
                          onClick={() => selectContract(contract.id)}
                        >
                          <span>
                            {copy.contract} {shortId(contract.id)}
                          </span>
                          <StatusBadge
                            label={copy.statusLabels[contract.status]}
                            status={contract.status}
                          />
                        </button>
                      ))}
                    </div>

                    {!selectedContractId && (
                      <EmptyState title={copy.chooseContract} compact />
                    )}
                  </>
                )}

                {workspace?.status === 'loading' && (
                  <p className="state-message workspace-loading" role="status">
                    {copy.loadingAssessment}
                  </p>
                )}

                {workspace?.status === 'error' && (
                  <ErrorState
                    message={errorMessage(workspace.error, language)}
                    retryLabel={copy.retry}
                    onRetry={retryWorkspace}
                  />
                )}

                {workspace?.status === 'ready' && (
                  <>
                    <ContractWorkspaceView
                      assessment={workspace.data.assessment}
                      contract={workspace.data.details}
                      createdRequest={createdRequest}
                      language={language}
                      onCreateRequest={openConfirmation}
                    />
                    <AssistantPanel
                      answer={assistantAnswer}
                      error={assistantError}
                      isLoading={askingAssistant}
                      language={language}
                      onAsk={askAssistant}
                      onQuestionChange={setAssistantQuestion}
                      question={assistantQuestion}
                    />
                  </>
                )}
              </>
            )}
          </div>
        </section>
      </main>

      <footer>
        <span>ContractIQ</span>
        <span>{copy.footerPrinciple}</span>
      </footer>

      {confirmationOpen && workspace?.status === 'ready' && (
        <div className="dialog-backdrop" onMouseDown={closeConfirmation}>
          <section
            aria-labelledby="confirmation-title"
            aria-modal="true"
            className="confirmation-dialog"
            onMouseDown={(event) => event.stopPropagation()}
            role="dialog"
          >
            <p className="step-number">03</p>
            <h2 id="confirmation-title">{copy.confirmationTitle}</h2>
            <p>{copy.confirmationDescription}</p>

            <div className="confirmation-summary">
              <span>{copy.earliestTermination}</span>
              <strong>
                {formatDate(
                  workspace.data.assessment.earliestTerminationDate,
                  language,
                )}
              </strong>
              <span>{copy.estimatedPenalty}</span>
              <strong>
                {formatMoney(workspace.data.assessment.penalty, language)}
              </strong>
            </div>

            <label className="confirmation-check">
              <input
                checked={assessmentConfirmed}
                onChange={(event) => {
                  setAssessmentConfirmed(event.target.checked)
                  setConfirmationError(undefined)
                }}
                type="checkbox"
              />
              <span>{copy.confirmationCheck}</span>
            </label>

            {confirmationError && (
              <p className="inline-error" role="alert">
                {confirmationError}
              </p>
            )}

            <div className="dialog-actions">
              <button
                className="secondary-button"
                disabled={submitting}
                onClick={closeConfirmation}
                type="button"
              >
                {copy.cancel}
              </button>
              <button
                className="primary-button"
                disabled={submitting}
                onClick={confirmCancellation}
                type="button"
              >
                {submitting ? copy.creating : copy.confirm}
              </button>
            </div>
          </section>
        </div>
      )}
    </div>
  )
}

function AssistantPanel({
  answer,
  error,
  isLoading,
  language,
  onAsk,
  onQuestionChange,
  question,
}: {
  answer?: ContractAnswer
  error?: string
  isLoading: boolean
  language: Language
  onAsk: () => void
  onQuestionChange: (value: string) => void
  question: string
}) {
  const copy = translations[language]

  return (
    <article className="assistant-card">
      <div className="assistant-heading">
        <div>
          <p className="step-number">03</p>
          <h3>{copy.assistantTitle}</h3>
        </div>
        <span>AI + RAG</span>
      </div>
      <p className="assistant-description">{copy.assistantDescription}</p>

      <label className="assistant-question">
        <span>{copy.assistantQuestionLabel}</span>
        <textarea
          maxLength={1000}
          onChange={(event) => onQuestionChange(event.target.value)}
          placeholder={copy.assistantPlaceholder}
          rows={3}
          value={question}
        />
      </label>

      <button
        className="primary-button"
        disabled={isLoading || question.trim().length < 3}
        onClick={onAsk}
        type="button"
      >
        {isLoading ? copy.askingAssistant : copy.askAssistant}
      </button>

      {error && (
        <p className="inline-error" role="alert">
          {error}
        </p>
      )}

      {answer && (
        <section
          className="assistant-response"
          data-grounded={answer.hasSufficientEvidence}
          aria-live="polite"
        >
          <p className="assistant-response-label">
            {answer.hasSufficientEvidence
              ? copy.assistantAnswer
              : copy.insufficientEvidence}
          </p>
          <p className="assistant-answer-text">{answer.answer}</p>

          {answer.citations.length > 0 && (
            <div className="citation-list">
              <strong>{copy.assistantSources}</strong>
              <ol>
                {answer.citations.map((citation) => (
                  <li key={`${citation.number}-${citation.documentKey}`}>
                    <span>[{citation.number}]</span>
                    <div>
                      <strong>{citation.title}</strong>
                      <small>
                        {copy.version} {citation.version} · {citation.section} ·{' '}
                        {copy.page} {citation.page}
                      </small>
                    </div>
                  </li>
                ))}
              </ol>
            </div>
          )}
        </section>
      )}
    </article>
  )
}

function ContractWorkspaceView({
  assessment,
  contract,
  createdRequest,
  language,
  onCreateRequest,
}: {
  assessment: CancellationAssessment
  contract: ContractDetails
  createdRequest?: CancellationRequest
  language: Language
  onCreateRequest: () => void
}) {
  const copy = translations[language]

  return (
    <div className="workspace-grid">
      <article className="detail-card">
        <div className="card-heading">
          <h3>{copy.contractDetails}</h3>
          <StatusBadge
            label={copy.statusLabels[contract.status]}
            status={contract.status}
          />
        </div>
        <dl className="detail-list">
          <Detail
            label={copy.monthlyFee}
            value={formatMoney(contract.monthlyFee, language)}
          />
          <Detail
            label={copy.startDate}
            value={formatDate(contract.startDate, language)}
          />
          <Detail
            label={copy.commitmentEnd}
            value={formatDate(contract.minimumCommitmentEndDate, language)}
          />
          <Detail
            label={copy.noticePeriod}
            value={`${contract.noticePeriodDays} ${copy.days}`}
          />
          <Detail
            label={copy.earlyTerminationRate}
            value={new Intl.NumberFormat(language, { style: 'percent' }).format(
              contract.earlyTerminationPenaltyRate,
            )}
          />
        </dl>
      </article>

      <article className="assessment-card" data-allowed={assessment.isAllowed}>
        <p className="assessment-label">{copy.assessment}</p>
        <h3>{assessment.isAllowed ? copy.eligible : copy.notEligible}</h3>
        <p>{copy.reasonLabels[assessment.reason]}</p>

        <dl className="assessment-values">
          <Detail
            label={copy.earliestTermination}
            value={formatDate(assessment.earliestTerminationDate, language)}
          />
          <Detail
            label={copy.remainingPeriods}
            value={String(assessment.chargeableMonthlyPeriods)}
          />
          <Detail
            label={copy.estimatedPenalty}
            value={
              assessment.hasPenalty
                ? formatMoney(assessment.penalty, language)
                : copy.noPenalty
            }
          />
        </dl>

        {assessment.isAllowed && !createdRequest && (
          <button
            className="primary-button full-width"
            onClick={onCreateRequest}
            type="button"
          >
            {copy.createRequest}
          </button>
        )}

        {createdRequest && (
          <div className="success-message" role="status">
            <span className="success-icon" aria-hidden="true">
              ✓
            </span>
            <div>
              <strong>{copy.successTitle}</strong>
              <p>{copy.successDescription}</p>
              <dl>
                <Detail
                  label={copy.requestId}
                  value={shortId(createdRequest.id)}
                />
                <Detail label={copy.status} value={copy.pendingReview} />
              </dl>
            </div>
          </div>
        )}
      </article>
    </div>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}

function StatusBadge({ label, status }: { label: string; status: string }) {
  return (
    <span className="status-badge" data-status={status}>
      {label}
    </span>
  )
}

function EmptyState({
  title,
  description,
  compact = false,
}: {
  title: string
  description?: string
  compact?: boolean
}) {
  return (
    <div className="empty-state" data-compact={compact}>
      <span aria-hidden="true">⌁</span>
      <h3>{title}</h3>
      {description && <p>{description}</p>}
    </div>
  )
}

function ErrorState({
  message,
  retryLabel,
  onRetry,
}: {
  message: string
  retryLabel: string
  onRetry: () => void
}) {
  return (
    <div className="error-state" role="alert">
      <strong>{message}</strong>
      <button className="text-button" onClick={onRetry} type="button">
        {retryLabel}
      </button>
    </div>
  )
}
