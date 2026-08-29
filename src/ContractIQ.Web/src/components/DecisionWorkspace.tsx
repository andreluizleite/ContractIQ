import type { CancellationRequest, CustomerSummary } from '../api'
import { formatDate, formatMoney, shortId } from '../formatters'
import type { ContractWorkspace, Loadable } from '../hooks/useContractWorkspace'
import { translations, type Language } from '../translations'
import { Detail, EmptyState, ErrorState, Icon, StatusBadge } from './ui'

export function DecisionWorkspace({
  createdRequest,
  language,
  getErrorMessage,
  onCreateRequest,
  onRetry,
  selectedContractId,
  selectedCustomer,
  workspace,
}: {
  createdRequest?: CancellationRequest
  language: Language
  getErrorMessage: (error: unknown) => string
  onCreateRequest: () => void
  onRetry: () => void
  selectedContractId?: string
  selectedCustomer?: CustomerSummary
  workspace: Loadable<ContractWorkspace> | null
}) {
  const copy = translations[language]

  if (!selectedCustomer) {
    return (
      <main className="decision-workspace">
        <EmptyState
          description={copy.chooseCustomerDescription}
          icon="building"
          title={copy.chooseCustomer}
        />
      </main>
    )
  }

  if (!selectedContractId) {
    return (
      <main className="decision-workspace">
        <header className="workspace-context-header">
          <ContextHeader customer={selectedCustomer} language={language} />
        </header>
        <EmptyState
          description={copy.chooseContractDescription}
          title={copy.chooseContract}
        />
      </main>
    )
  }

  return (
    <main className="decision-workspace">
      <header className="workspace-context-header">
        <ContextHeader
          contractId={selectedContractId}
          customer={selectedCustomer}
          language={language}
        />
      </header>

      {workspace?.status === 'loading' && (
        <div className="workspace-loading" role="status">
          <span className="loading-orbit" aria-hidden="true" />
          <strong>{copy.loadingAssessment}</strong>
          <small>{copy.deterministicProcessing}</small>
        </div>
      )}

      {workspace?.status === 'error' && (
        <ErrorState
          message={getErrorMessage(workspace.error)}
          onRetry={onRetry}
          retryLabel={copy.retry}
        />
      )}

      {workspace?.status === 'ready' && (
        <div className="decision-content">
          <section
            className="decision-summary"
            aria-labelledby="decision-title"
            data-allowed={workspace.data.assessment.isAllowed}
          >
            <div className="decision-summary-heading">
              <span className="decision-icon">
                <Icon
                  name={
                    workspace.data.assessment.isAllowed ? 'shield' : 'blocked'
                  }
                />
              </span>
              <div>
                <p>{copy.deterministicDecision}</p>
                <h2 id="decision-title">
                  {workspace.data.assessment.isAllowed
                    ? copy.eligible
                    : copy.notEligible}
                </h2>
              </div>
              <span
                className="rules-badge"
                data-allowed={workspace.data.assessment.isAllowed}
              >
                <Icon
                  name={
                    workspace.data.assessment.isAllowed ? 'check' : 'blocked'
                  }
                />
                {copy.calculatedByRules}
              </span>
            </div>

            <p className="decision-reason">
              {copy.reasonLabels[workspace.data.assessment.reason]}
            </p>

            <dl className="decision-values">
              <Detail
                icon="calendar"
                label={copy.earliestTermination}
                value={formatDate(
                  workspace.data.assessment.earliestTerminationDate,
                  language,
                )}
              />
              <Detail
                icon="contract"
                label={copy.remainingPeriods}
                value={String(
                  workspace.data.assessment.chargeableMonthlyPeriods,
                )}
              />
              <Detail
                icon="money"
                label={copy.estimatedPenalty}
                value={
                  workspace.data.assessment.hasPenalty
                    ? formatMoney(workspace.data.assessment.penalty, language)
                    : copy.noPenalty
                }
              />
            </dl>

            {workspace.data.assessment.hasPenalty && (
              <p className="penalty-breakdown">
                <span>{copy.penaltyCalculation}</span>
                <strong>
                  {workspace.data.assessment.chargeableMonthlyPeriods} ×{' '}
                  {formatMoney(workspace.data.details.monthlyFee, language)} ×{' '}
                  {new Intl.NumberFormat(language, {
                    style: 'percent',
                  }).format(workspace.data.details.earlyTerminationPenaltyRate)}
                </strong>
              </p>
            )}

            {workspace.data.assessment.isAllowed && !createdRequest && (
              <button
                className="primary-button decision-action"
                onClick={onCreateRequest}
                type="button"
              >
                {copy.createRequest}
                <Icon name="chevron" />
              </button>
            )}

            {createdRequest && (
              <div className="success-message" role="status">
                <span className="success-icon">
                  <Icon name="check" />
                </span>
                <div>
                  <strong>{copy.successTitle}</strong>
                  <p>{copy.successDescription}</p>
                  <div className="success-metadata">
                    <span>
                      {copy.requestId}: {shortId(createdRequest.id)}
                    </span>
                    <span>{copy.pendingReview}</span>
                  </div>
                </div>
              </div>
            )}
          </section>

          <section
            className="contract-information"
            aria-labelledby="contract-information-title"
          >
            <div className="section-heading">
              <div>
                <p>{copy.contractRecord}</p>
                <h2 id="contract-information-title">{copy.contractDetails}</h2>
              </div>
              <StatusBadge
                label={copy.statusLabels[workspace.data.details.status]}
                status={workspace.data.details.status}
              />
            </div>
            <dl className="contract-detail-grid">
              <Detail
                label={copy.monthlyFee}
                value={formatMoney(workspace.data.details.monthlyFee, language)}
              />
              <Detail
                label={copy.startDate}
                value={formatDate(workspace.data.details.startDate, language)}
              />
              <Detail
                label={copy.commitmentEnd}
                value={formatDate(
                  workspace.data.details.minimumCommitmentEndDate,
                  language,
                )}
              />
              <Detail
                label={copy.noticePeriod}
                value={`${workspace.data.details.noticePeriodDays} ${copy.days}`}
              />
              <Detail
                label={copy.earlyTerminationRate}
                value={new Intl.NumberFormat(language, {
                  style: 'percent',
                }).format(workspace.data.details.earlyTerminationPenaltyRate)}
              />
            </dl>
          </section>

          <aside className="principle-note">
            <Icon name="shield" />
            <div>
              <strong>{copy.domainPrincipleTitle}</strong>
              <p>{copy.footerPrinciple}</p>
            </div>
          </aside>
        </div>
      )}
    </main>
  )
}

function ContextHeader({
  contractId,
  customer,
  language,
}: {
  contractId?: string
  customer: CustomerSummary
  language: Language
}) {
  const copy = translations[language]

  return (
    <div>
      <p className="context-eyebrow">
        {copy.workspaceTitle}
        {contractId && ` / ${copy.contract} ${shortId(contractId)}`}
      </p>
      <div className="context-title-row">
        <div>
          <h1>{customer.name}</h1>
          <p>{copy.workspaceDescription}</p>
        </div>
        <span className="customer-context-avatar" aria-hidden="true">
          {customer.name.slice(0, 2).toUpperCase()}
        </span>
      </div>
    </div>
  )
}
