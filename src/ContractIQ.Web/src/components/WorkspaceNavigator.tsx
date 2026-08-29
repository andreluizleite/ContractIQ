import type { RefObject } from 'react'

import type { ContractSummary, CustomerSummary } from '../api'
import type { Loadable } from '../hooks/useContractWorkspace'
import { translations, type Language } from '../translations'
import { ErrorState, Icon, StatusBadge } from './ui'

function shortId(id: string) {
  return id.slice(0, 8).toUpperCase()
}

function formatMoney(amount: number, currency: string, language: Language) {
  return new Intl.NumberFormat(language, {
    style: 'currency',
    currency,
    maximumFractionDigits: 0,
  }).format(amount)
}

export function WorkspaceNavigator({
  contracts,
  closeButtonRef,
  customerSearch,
  customers,
  language,
  getErrorMessage,
  isCompact,
  isOpen,
  onCustomerSearchChange,
  onLanguageChange,
  onClose,
  onRetryContracts,
  onRetryCustomers,
  onSelectContract,
  onSelectCustomer,
  selectedContractId,
  selectedCustomer,
  selectedCustomerId,
}: {
  contracts: Loadable<ContractSummary[]> | null
  closeButtonRef: RefObject<HTMLButtonElement | null>
  customerSearch: string
  customers: Loadable<CustomerSummary[]>
  language: Language
  getErrorMessage: (error: unknown) => string
  isCompact: boolean
  isOpen: boolean
  onCustomerSearchChange: (value: string) => void
  onLanguageChange: (language: Language) => void
  onClose: () => void
  onRetryContracts: () => void
  onRetryCustomers: () => void
  onSelectContract: (id: string) => void
  onSelectCustomer: (id: string) => void
  selectedContractId?: string
  selectedCustomer?: CustomerSummary
  selectedCustomerId?: string
}) {
  const copy = translations[language]
  const normalizedSearch = customerSearch.trim().toLocaleLowerCase(language)
  const visibleCustomers =
    customers.status === 'ready'
      ? customers.data.filter((customer) =>
          customer.name.toLocaleLowerCase(language).includes(normalizedSearch),
        )
      : []

  return (
    <aside
      aria-hidden={isCompact && !isOpen}
      aria-label={copy.navigationLabel}
      className="workspace-navigator"
      data-open={isOpen}
      inert={isCompact && !isOpen}
      aria-modal={isCompact && isOpen ? true : undefined}
      role={isCompact ? 'dialog' : undefined}
    >
      <button
        aria-label={copy.closeNavigator}
        className="drawer-close-button navigator-close-button"
        onClick={onClose}
        ref={closeButtonRef}
        type="button"
      >
        <Icon name="blocked" />
      </button>
      <div className="product-brand">
        <span className="product-brand-mark">
          <Icon name="contract" />
        </span>
        <span>
          <strong>ContractIQ</strong>
          <small>{copy.productSubtitle}</small>
        </span>
      </div>

      <div className="navigator-heading">
        <span className="section-icon">
          <Icon name="building" />
        </span>
        <div>
          <p>{copy.workspaceNav}</p>
          <h1>{copy.customers}</h1>
        </div>
      </div>

      {customers.status === 'ready' && customers.data.length > 0 && (
        <label className="search-field">
          <span className="visually-hidden">{copy.searchCustomers}</span>
          <Icon name="search" />
          <input
            onChange={(event) => onCustomerSearchChange(event.target.value)}
            placeholder={copy.searchCustomersPlaceholder}
            type="search"
            value={customerSearch}
          />
        </label>
      )}

      <div className="navigator-scroll-region">
        {customers.status === 'loading' && (
          <div className="loading-list" role="status">
            <span className="visually-hidden">{copy.loadingCustomers}</span>
            <span />
            <span />
            <span />
          </div>
        )}

        {customers.status === 'error' && (
          <ErrorState
            message={getErrorMessage(customers.error)}
            onRetry={onRetryCustomers}
            retryLabel={copy.retry}
          />
        )}

        {customers.status === 'ready' && customers.data.length === 0 && (
          <p className="navigator-message">{copy.noCustomers}</p>
        )}

        {customers.status === 'ready' && visibleCustomers.length === 0 && (
          <p className="navigator-message">{copy.noCustomerSearchResults}</p>
        )}

        {visibleCustomers.length > 0 && (
          <div className="customer-list">
            {visibleCustomers.map((customer) => (
              <button
                aria-pressed={customer.id === selectedCustomerId}
                className="customer-button"
                key={customer.id}
                onClick={() => onSelectCustomer(customer.id)}
                type="button"
              >
                <span className="customer-avatar" aria-hidden="true">
                  {customer.name.slice(0, 2).toUpperCase()}
                </span>
                <span className="navigator-item-copy">
                  <strong>{customer.name}</strong>
                  <small>{copy.customerAccount}</small>
                </span>
                <Icon className="chevron-icon" name="chevron" />
              </button>
            ))}
          </div>
        )}

        {selectedCustomer && (
          <section
            className="contract-navigation"
            aria-labelledby="contract-navigation-title"
          >
            <div className="contract-navigation-heading">
              <p id="contract-navigation-title">{copy.contracts}</p>
              {contracts?.status === 'ready' && (
                <span>{contracts.data.length}</span>
              )}
            </div>

            {contracts?.status === 'loading' && (
              <p className="navigator-message" role="status">
                {copy.loadingContracts}
              </p>
            )}

            {contracts?.status === 'error' && (
              <ErrorState
                message={getErrorMessage(contracts.error)}
                onRetry={onRetryContracts}
                retryLabel={copy.retry}
              />
            )}

            {contracts?.status === 'ready' && contracts.data.length === 0 && (
              <p className="navigator-message">{copy.noContracts}</p>
            )}

            {contracts?.status === 'ready' && contracts.data.length > 0 && (
              <div className="contract-list">
                {contracts.data.map((contract) => (
                  <button
                    aria-pressed={contract.id === selectedContractId}
                    className="contract-button"
                    key={contract.id}
                    onClick={() => onSelectContract(contract.id)}
                    type="button"
                  >
                    <span className="contract-button-icon">
                      <Icon name="contract" />
                    </span>
                    <span className="navigator-item-copy">
                      <strong>
                        {copy.contract} {shortId(contract.id)}
                      </strong>
                      <small>
                        {formatMoney(
                          contract.monthlyFee.amount,
                          contract.monthlyFee.currency,
                          language,
                        )}
                      </small>
                    </span>
                    <StatusBadge
                      label={copy.statusLabels[contract.status]}
                      status={contract.status}
                    />
                  </button>
                ))}
              </div>
            )}
          </section>
        )}
      </div>

      <div className="navigator-footer">
        <span className="environment-indicator" aria-hidden="true" />
        <span>{copy.localEnvironment}</span>
        <label className="language-control">
          <span className="visually-hidden">{copy.languageLabel}</span>
          <Icon name="globe" />
          <select
            aria-label={copy.languageLabel}
            onChange={(event) =>
              onLanguageChange(event.target.value as Language)
            }
            value={language}
          >
            <option value="en">EN</option>
            <option value="pt-BR">PT-BR</option>
          </select>
        </label>
      </div>
    </aside>
  )
}
