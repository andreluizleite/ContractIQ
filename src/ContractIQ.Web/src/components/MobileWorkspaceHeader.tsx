import type { RefObject } from 'react'

import type { CustomerSummary } from '../api'
import { shortId } from '../formatters'
import { translations, type Language } from '../translations'
import { Icon } from './ui'

export function MobileWorkspaceHeader({
  contractId,
  customer,
  language,
  navigatorButtonRef,
  onOpenCopilot,
  onOpenNavigator,
  copilotButtonRef,
}: {
  contractId?: string
  customer?: CustomerSummary
  language: Language
  navigatorButtonRef: RefObject<HTMLButtonElement | null>
  onOpenCopilot: () => void
  onOpenNavigator: () => void
  copilotButtonRef: RefObject<HTMLButtonElement | null>
}) {
  const copy = translations[language]

  return (
    <header className="mobile-workspace-header">
      <button
        aria-label={copy.openNavigator}
        className="mobile-header-button"
        onClick={onOpenNavigator}
        ref={navigatorButtonRef}
        type="button"
      >
        <Icon name="building" />
      </button>

      <div className="mobile-context-copy">
        <strong>{customer?.name ?? copy.workspaceTitle}</strong>
        <small>
          {contractId
            ? `${copy.contract} ${shortId(contractId)}`
            : copy.mobileContextPrompt}
        </small>
      </div>

      <button
        aria-label={copy.openCopilot}
        className="mobile-header-button copilot-button"
        disabled={!contractId}
        onClick={onOpenCopilot}
        ref={copilotButtonRef}
        type="button"
      >
        <Icon name="ai" />
      </button>
    </header>
  )
}
