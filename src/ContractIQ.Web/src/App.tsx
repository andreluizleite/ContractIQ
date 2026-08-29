import { useEffect, useRef, useState } from 'react'

import { ApiError, contractIqApi, type CancellationRequest } from './api'
import { AssistantCopilot } from './components/AssistantCopilot'
import { CancellationDialog } from './components/CancellationDialog'
import { DecisionWorkspace } from './components/DecisionWorkspace'
import { MobileWorkspaceHeader } from './components/MobileWorkspaceHeader'
import { WorkspaceNavigator } from './components/WorkspaceNavigator'
import { useContractAssistant } from './hooks/useContractAssistant'
import { useContractWorkspace } from './hooks/useContractWorkspace'
import { translations, type Language } from './translations'

type ConfirmationSource = 'manual' | 'assistant'
type WorkspaceDrawer = 'navigator' | 'copilot'

function isCompactWorkspace() {
  return (
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(max-width: 1240px)').matches
  )
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
  const [customerSearch, setCustomerSearch] = useState('')
  const [createdRequest, setCreatedRequest] = useState<CancellationRequest>()
  const [confirmationOpen, setConfirmationOpen] = useState(false)
  const [assessmentConfirmed, setAssessmentConfirmed] = useState(false)
  const [confirmationError, setConfirmationError] = useState<string>()
  const [submitting, setSubmitting] = useState(false)
  const [idempotencyKey, setIdempotencyKey] = useState<string>()
  const [confirmationSource, setConfirmationSource] =
    useState<ConfirmationSource>('manual')
  const [compactWorkspace, setCompactWorkspace] = useState(isCompactWorkspace)
  const [activeDrawer, setActiveDrawer] = useState<WorkspaceDrawer>()
  const navigatorButtonRef = useRef<HTMLButtonElement>(null)
  const copilotButtonRef = useRef<HTMLButtonElement>(null)
  const navigatorCloseButtonRef = useRef<HTMLButtonElement>(null)
  const copilotCloseButtonRef = useRef<HTMLButtonElement>(null)
  const contractWorkspace = useContractWorkspace()
  const assistant = useContractAssistant({
    contractId: contractWorkspace.selectedContractId,
    customerId: contractWorkspace.selectedCustomerId,
    getErrorMessage: (error) => errorMessage(error, language),
    language,
  })
  const copy = translations[language]

  useEffect(() => {
    if (typeof window.matchMedia !== 'function') {
      return
    }

    const mediaQuery = window.matchMedia('(max-width: 1240px)')

    function onMediaChange(event: MediaQueryListEvent) {
      setCompactWorkspace(event.matches)
      if (!event.matches) {
        setActiveDrawer(undefined)
      }
    }

    mediaQuery.addEventListener('change', onMediaChange)

    return () => mediaQuery.removeEventListener('change', onMediaChange)
  }, [])

  useEffect(() => {
    if (!activeDrawer) {
      return
    }

    const trigger =
      activeDrawer === 'navigator'
        ? navigatorButtonRef.current
        : copilotButtonRef.current
    const closeButton =
      activeDrawer === 'navigator'
        ? navigatorCloseButtonRef.current
        : copilotCloseButtonRef.current
    const previousOverflow = document.body.style.overflow
    const focusFrame = requestAnimationFrame(() => closeButton?.focus())

    document.body.style.overflow = 'hidden'

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        event.preventDefault()
        setActiveDrawer(undefined)
        return
      }

      if (event.key !== 'Tab') {
        return
      }

      const drawer = closeButton?.closest<HTMLElement>('aside')
      if (!drawer) {
        return
      }

      const focusable = Array.from(
        drawer.querySelectorAll<HTMLElement>(
          ':is(button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [href], [tabindex]:not([tabindex="-1"]))',
        ),
      )
      const first = focusable[0]
      const last = focusable[focusable.length - 1]

      if (event.shiftKey && document.activeElement === first) {
        event.preventDefault()
        last?.focus()
      } else if (!event.shiftKey && document.activeElement === last) {
        event.preventDefault()
        first?.focus()
      }
    }

    document.addEventListener('keydown', onKeyDown)

    return () => {
      cancelAnimationFrame(focusFrame)
      document.removeEventListener('keydown', onKeyDown)
      document.body.style.overflow = previousOverflow
      trigger?.focus()
    }
  }, [activeDrawer])

  function changeLanguage(nextLanguage: Language) {
    setLanguage(nextLanguage)
    assistant.reset()
    document.documentElement.lang = nextLanguage
  }

  function selectCustomer(customerId: string) {
    contractWorkspace.selectCustomer(customerId)
    setCreatedRequest(undefined)
    assistant.reset()
  }

  function selectContract(contractId: string) {
    contractWorkspace.selectContract(contractId)
    setCreatedRequest(undefined)
    assistant.reset()
  }

  function openConfirmation(source: ConfirmationSource = 'manual') {
    setAssessmentConfirmed(false)
    setConfirmationError(undefined)
    setIdempotencyKey(globalThis.crypto.randomUUID())
    setConfirmationSource(source)
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

    if (!contractWorkspace.selectedContractId || !idempotencyKey) {
      return
    }

    setSubmitting(true)
    setConfirmationError(undefined)

    try {
      const request =
        confirmationSource === 'assistant' &&
        contractWorkspace.selectedCustomerId &&
        assistant.answer?.proposedAction
          ? await contractIqApi.confirmAssistantCancellation(
              contractWorkspace.selectedCustomerId,
              contractWorkspace.selectedContractId,
              assistant.answer.proposedAction.intent,
              assessmentConfirmed,
              idempotencyKey,
            )
          : await contractIqApi.createCancellationRequest(
              contractWorkspace.selectedContractId,
              idempotencyKey,
            )
      setCreatedRequest(request)
      assistant.clearProposedAction()
      setConfirmationOpen(false)
    } catch (error) {
      setConfirmationError(errorMessage(error, language))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="workspace-shell">
      <WorkspaceNavigator
        closeButtonRef={navigatorCloseButtonRef}
        contracts={contractWorkspace.contracts}
        customerSearch={customerSearch}
        customers={contractWorkspace.customers}
        getErrorMessage={(error) => errorMessage(error, language)}
        isCompact={compactWorkspace}
        isOpen={activeDrawer === 'navigator'}
        language={language}
        onCustomerSearchChange={setCustomerSearch}
        onClose={() => setActiveDrawer(undefined)}
        onLanguageChange={changeLanguage}
        onRetryContracts={contractWorkspace.retryContracts}
        onRetryCustomers={contractWorkspace.retryCustomers}
        onSelectContract={selectContract}
        onSelectCustomer={selectCustomer}
        selectedContractId={contractWorkspace.selectedContractId}
        selectedCustomer={contractWorkspace.selectedCustomer}
        selectedCustomerId={contractWorkspace.selectedCustomerId}
      />

      <MobileWorkspaceHeader
        contractId={contractWorkspace.selectedContractId}
        copilotButtonRef={copilotButtonRef}
        customer={contractWorkspace.selectedCustomer}
        language={language}
        navigatorButtonRef={navigatorButtonRef}
        onOpenCopilot={() => setActiveDrawer('copilot')}
        onOpenNavigator={() => setActiveDrawer('navigator')}
      />

      <DecisionWorkspace
        createdRequest={createdRequest}
        getErrorMessage={(error) => errorMessage(error, language)}
        language={language}
        onCreateRequest={() => openConfirmation('manual')}
        onRetry={contractWorkspace.retryWorkspace}
        selectedContractId={contractWorkspace.selectedContractId}
        selectedCustomer={contractWorkspace.selectedCustomer}
        workspace={contractWorkspace.workspace}
      />

      <AssistantCopilot
        answer={assistant.answer}
        closeButtonRef={copilotCloseButtonRef}
        contractId={contractWorkspace.selectedContractId}
        customer={contractWorkspace.selectedCustomer}
        error={assistant.error}
        isCompact={compactWorkspace}
        isLoading={assistant.isLoading}
        isOpen={activeDrawer === 'copilot'}
        language={language}
        onAsk={assistant.ask}
        onClose={() => setActiveDrawer(undefined)}
        onQuestionChange={assistant.setQuestion}
        onReviewAction={() => openConfirmation('assistant')}
        question={assistant.question}
        requestCreated={Boolean(createdRequest)}
        submittedQuestion={assistant.submittedQuestion}
      />

      {activeDrawer && compactWorkspace && (
        <button
          aria-label={copy.closeOverlay}
          className="drawer-backdrop"
          onClick={() => setActiveDrawer(undefined)}
          tabIndex={-1}
          type="button"
        />
      )}

      {confirmationOpen && contractWorkspace.workspace?.status === 'ready' && (
        <CancellationDialog
          assessment={contractWorkspace.workspace.data.assessment}
          confirmed={assessmentConfirmed}
          contractId={contractWorkspace.selectedContractId!}
          customerName={contractWorkspace.selectedCustomer?.name ?? ''}
          error={confirmationError}
          language={language}
          onClose={closeConfirmation}
          onConfirm={confirmCancellation}
          onConfirmedChange={(confirmed) => {
            setAssessmentConfirmed(confirmed)
            setConfirmationError(undefined)
          }}
          source={confirmationSource}
          submitting={submitting}
        />
      )}
    </div>
  )
}
