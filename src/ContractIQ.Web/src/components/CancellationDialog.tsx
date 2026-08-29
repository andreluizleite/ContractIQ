import {
  useEffect,
  useRef,
  type KeyboardEvent as ReactKeyboardEvent,
} from 'react'

import type { CancellationAssessment } from '../api'
import { formatDate, formatMoney } from '../formatters'
import { translations, type Language } from '../translations'
import { Icon } from './ui'

export function CancellationDialog({
  assessment,
  confirmed,
  contractId,
  customerName,
  error,
  language,
  onClose,
  onConfirm,
  onConfirmedChange,
  source,
  submitting,
}: {
  assessment: CancellationAssessment
  confirmed: boolean
  contractId: string
  customerName: string
  error?: string
  language: Language
  onClose: () => void
  onConfirm: () => void
  onConfirmedChange: (confirmed: boolean) => void
  source: 'manual' | 'assistant'
  submitting: boolean
}) {
  const copy = translations[language]
  const dialogRef = useRef<HTMLElement>(null)
  useEffect(() => {
    const previouslyFocused = document.activeElement as HTMLElement | null
    const dialog = dialogRef.current
    const previousOverflow = document.body.style.overflow

    document.body.style.overflow = 'hidden'
    dialog?.focus()

    return () => {
      document.body.style.overflow = previousOverflow
      previouslyFocused?.focus()
    }
  }, [])

  function handleKeyDown(event: ReactKeyboardEvent<HTMLElement>) {
    if (event.key === 'Escape' && !submitting) {
      event.preventDefault()
      onClose()
      return
    }

    if (event.key !== 'Tab') {
      return
    }

    const dialog = event.currentTarget
    const focusable = Array.from(
      dialog.querySelectorAll<HTMLElement>(
        ':is(button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [href], [tabindex]:not([tabindex="-1"]))',
      ),
    )

    if (focusable.length === 0) {
      event.preventDefault()
      dialog.focus()
      return
    }

    const first = focusable[0]
    const last = focusable[focusable.length - 1]

    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault()
      last.focus()
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault()
      first.focus()
    }
  }

  return (
    <div className="dialog-backdrop" onMouseDown={onClose}>
      <section
        aria-describedby="confirmation-description"
        aria-labelledby="confirmation-title"
        aria-modal="true"
        className="confirmation-dialog"
        onKeyDown={handleKeyDown}
        onMouseDown={(event) => event.stopPropagation()}
        ref={dialogRef}
        role="dialog"
        tabIndex={-1}
      >
        <div className="dialog-icon">
          <Icon name="shield" />
        </div>
        <p className="dialog-eyebrow">{copy.secureAction}</p>
        <h2 id="confirmation-title">{copy.confirmationTitle}</h2>
        <p id="confirmation-description">
          {source === 'assistant'
            ? copy.agentConfirmationDescription
            : copy.confirmationDescription}
        </p>

        <dl className="confirmation-summary">
          <div>
            <dt>{copy.confirmationCustomer}</dt>
            <dd>{customerName}</dd>
          </div>
          <div>
            <dt>{copy.confirmationContract}</dt>
            <dd>{contractId.slice(0, 8).toUpperCase()}</dd>
          </div>
          <div>
            <dt>{copy.earliestTermination}</dt>
            <dd>{formatDate(assessment.earliestTerminationDate, language)}</dd>
          </div>
          <div>
            <dt>{copy.estimatedPenalty}</dt>
            <dd>{formatMoney(assessment.penalty, language)}</dd>
          </div>
          <div>
            <dt>{copy.confirmationOrigin}</dt>
            <dd>
              {source === 'assistant'
                ? copy.assistantOrigin
                : copy.manualOrigin}
            </dd>
          </div>
        </dl>

        <p className="confirmation-consequence">
          <Icon name="contract" />
          {copy.confirmationConsequence}
        </p>

        <label className="confirmation-check">
          <input
            checked={confirmed}
            onChange={(event) => onConfirmedChange(event.target.checked)}
            type="checkbox"
          />
          <span>
            <strong>{copy.reviewConfirmation}</strong>
            {copy.confirmationCheck}
          </span>
        </label>

        {error && (
          <p className="inline-error" role="alert">
            <Icon name="alert" />
            {error}
          </p>
        )}

        <div className="dialog-actions">
          <button
            className="secondary-button"
            disabled={submitting}
            onClick={onClose}
            type="button"
          >
            {copy.cancel}
          </button>
          <button
            className="primary-button"
            disabled={submitting}
            onClick={onConfirm}
            type="button"
          >
            {submitting ? copy.creating : copy.confirm}
          </button>
        </div>
      </section>
    </div>
  )
}
