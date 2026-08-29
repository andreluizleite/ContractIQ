import type { RefObject } from 'react'

import type { ContractAnswer, CustomerSummary } from '../api'
import { formatDate, formatMoney, shortId } from '../formatters'
import { translations, type Language } from '../translations'
import { Detail, Icon } from './ui'

export function AssistantCopilot({
  answer,
  closeButtonRef,
  contractId,
  customer,
  error,
  isLoading,
  isCompact,
  isOpen,
  language,
  onAsk,
  onClose,
  onQuestionChange,
  onReviewAction,
  question,
  requestCreated,
  submittedQuestion,
}: {
  answer?: ContractAnswer
  closeButtonRef: RefObject<HTMLButtonElement | null>
  contractId?: string
  customer?: CustomerSummary
  error?: string
  isLoading: boolean
  isCompact: boolean
  isOpen: boolean
  language: Language
  onAsk: () => void
  onClose: () => void
  onQuestionChange: (value: string) => void
  onReviewAction: () => void
  question: string
  requestCreated: boolean
  submittedQuestion?: string
}) {
  const copy = translations[language]
  const hasContext = Boolean(customer && contractId)

  return (
    <aside
      aria-hidden={isCompact && !isOpen}
      aria-labelledby="assistant-title"
      className="assistant-copilot"
      data-open={isOpen}
      inert={isCompact && !isOpen}
      aria-modal={isCompact && isOpen ? true : undefined}
      role={isCompact ? 'dialog' : undefined}
    >
      <button
        aria-label={copy.closeCopilot}
        className="drawer-close-button copilot-close-button"
        onClick={onClose}
        ref={closeButtonRef}
        type="button"
      >
        <Icon name="blocked" />
      </button>
      <header className="copilot-header">
        <span className="copilot-mark">
          <Icon name="ai" />
        </span>
        <div>
          <span className="copilot-kicker">{copy.assistantStatus}</span>
          <h2 id="assistant-title">{copy.assistantTitle}</h2>
        </div>
        <span className="copilot-mode">AI + RAG</span>
      </header>

      {hasContext ? (
        <>
          <div className="copilot-context">
            <span className="context-avatar" aria-hidden="true">
              {customer?.name.slice(0, 2).toUpperCase()}
            </span>
            <div>
              <strong>{customer?.name}</strong>
              <small>
                {copy.contract} {shortId(contractId!)}
              </small>
            </div>
            <span className="grounded-badge">
              <Icon name="shield" />
              RAG
            </span>
          </div>

          <div className="copilot-conversation" aria-live="polite">
            {!answer && !isLoading && (
              <div className="copilot-welcome">
                <span>
                  <Icon name="ai" />
                </span>
                <h3>{copy.copilotWelcome}</h3>
                <p>{copy.assistantDescription}</p>
              </div>
            )}

            {submittedQuestion && (
              <div className="conversation-message" data-role="user">
                <span>{copy.you}</span>
                <p>{submittedQuestion}</p>
              </div>
            )}

            {isLoading && (
              <div className="copilot-thinking" role="status">
                <span />
                <span />
                <span />
                <strong>{copy.askingAssistant}</strong>
              </div>
            )}

            {answer && (
              <AssistantResponse
                answer={answer}
                language={language}
                onReviewAction={onReviewAction}
                requestCreated={requestCreated}
              />
            )}
          </div>

          <div className="copilot-composer">
            <div className="suggested-questions">
              {[copy.cancellationQuestion, copy.penaltyQuestion].map(
                (suggestion) => (
                  <button
                    key={suggestion}
                    onClick={() => onQuestionChange(suggestion)}
                    type="button"
                  >
                    {suggestion}
                  </button>
                ),
              )}
              <button
                onClick={() => onQuestionChange(copy.createRequestQuestion)}
                type="button"
              >
                {copy.createRequestQuestion}
              </button>
            </div>

            <label className="assistant-question">
              <span className="visually-hidden">
                {copy.assistantQuestionLabel}
              </span>
              <textarea
                maxLength={1000}
                onChange={(event) => onQuestionChange(event.target.value)}
                placeholder={copy.assistantPlaceholder}
                rows={3}
                value={question}
              />
            </label>

            {error && (
              <p className="inline-error" role="alert">
                <Icon name="alert" />
                {error}
              </p>
            )}

            <div className="composer-footer">
              <span>
                <Icon name="shield" />
                {copy.groundedWithSources}
              </span>
              <button
                aria-label={copy.askAssistant}
                className="send-button"
                disabled={isLoading || question.trim().length < 3}
                onClick={onAsk}
                type="button"
              >
                <Icon name="chevron" />
              </button>
            </div>
          </div>
        </>
      ) : (
        <div className="copilot-empty">
          <span>
            <Icon name="contract" />
          </span>
          <h3>{copy.copilotNeedsContext}</h3>
          <p>{copy.copilotNeedsContextDescription}</p>
        </div>
      )}
    </aside>
  )
}

function AssistantResponse({
  answer,
  language,
  onReviewAction,
  requestCreated,
}: {
  answer: ContractAnswer
  language: Language
  onReviewAction: () => void
  requestCreated: boolean
}) {
  const copy = translations[language]

  return (
    <section
      className="assistant-response"
      data-grounded={answer.hasSufficientEvidence}
    >
      <div className="response-label">
        <Icon name={answer.hasSufficientEvidence ? 'shield' : 'alert'} />
        <span>
          {answer.hasSufficientEvidence
            ? copy.assistantAnswer
            : copy.insufficientEvidence}
        </span>
      </div>
      <p className="assistant-answer-text">{answer.answer}</p>

      {answer.proposedAction && (
        <div
          className="assistant-action"
          data-allowed={answer.proposedAction.canExecute}
        >
          <strong>{copy.actionPrepared}</strong>
          <p>
            {answer.proposedAction.canExecute
              ? copy.actionPreparedDescription
              : copy.actionNotAllowed}
          </p>
          <dl>
            <Detail
              label={copy.earliestTermination}
              value={formatDate(
                answer.proposedAction.assessment.earliestTerminationDate,
                language,
              )}
            />
            <Detail
              label={copy.estimatedPenalty}
              value={formatMoney(
                answer.proposedAction.assessment.penalty,
                language,
              )}
            />
          </dl>
          {answer.proposedAction.canExecute && !requestCreated && (
            <button
              className="primary-button"
              onClick={onReviewAction}
              type="button"
            >
              {copy.reviewAgentAction}
            </button>
          )}
        </div>
      )}

      {answer.citations.length > 0 && (
        <div className="citation-list">
          <div className="citation-heading">
            <strong>{copy.assistantSources}</strong>
            <span>{answer.citations.length}</span>
          </div>
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
  )
}
