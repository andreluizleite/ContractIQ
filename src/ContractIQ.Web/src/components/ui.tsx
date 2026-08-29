import type { ReactNode, SVGProps } from 'react'

export type IconName =
  | 'ai'
  | 'alert'
  | 'building'
  | 'blocked'
  | 'calendar'
  | 'check'
  | 'chevron'
  | 'contract'
  | 'globe'
  | 'money'
  | 'search'
  | 'shield'

const paths: Record<IconName, ReactNode> = {
  ai: (
    <>
      <path d="M12 3.5 13.8 8l4.7 1.8-4.7 1.8L12 16l-1.8-4.4-4.7-1.8L10.2 8 12 3.5Z" />
      <path d="m18.2 15 .8 2 .5.2-.5.2-.8 2-.8-2-.5-.2.5-.2.8-2Z" />
    </>
  ),
  alert: (
    <>
      <path d="M12 8v4" />
      <path d="M12 16h.01" />
      <path d="M10.3 3.7 2.8 17a2 2 0 0 0 1.7 3h15a2 2 0 0 0 1.7-3L13.7 3.7a2 2 0 0 0-3.4 0Z" />
    </>
  ),
  building: (
    <>
      <path d="M4 21V5a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v16" />
      <path d="M8 7h5M8 11h5M8 15h5M2 21h20M9 21v-3h3v3" />
    </>
  ),
  blocked: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="m6 6 12 12" />
    </>
  ),
  calendar: (
    <>
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M16 3v4M8 3v4M3 10h18" />
    </>
  ),
  check: <path d="m5 12 4 4L19 6" />,
  chevron: <path d="m9 18 6-6-6-6" />,
  contract: (
    <>
      <path d="M6 2h9l4 4v16H6a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2Z" />
      <path d="M14 2v5h5M8 12h8M8 16h6" />
    </>
  ),
  globe: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18" />
    </>
  ),
  money: (
    <>
      <circle cx="12" cy="12" r="9" />
      <path d="M16 8.5c-.8-.7-2-1-3.2-1-1.7 0-3 .8-3 2s1.1 1.8 3.2 2.3c2.1.5 3.2 1.1 3.2 2.3s-1.3 2.2-3.2 2.2c-1.4 0-2.7-.4-3.6-1.2M12.8 5.5v2M12.8 16.5v2" />
    </>
  ),
  search: (
    <>
      <circle cx="11" cy="11" r="7" />
      <path d="m20 20-4-4" />
    </>
  ),
  shield: (
    <>
      <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z" />
      <path d="m9 12 2 2 4-4" />
    </>
  ),
}

export function Icon({
  name,
  ...props
}: { name: IconName } & SVGProps<SVGSVGElement>) {
  return (
    <svg
      aria-hidden="true"
      fill="none"
      height="24"
      viewBox="0 0 24 24"
      width="24"
      {...props}
      stroke="currentColor"
      strokeLinecap="round"
      strokeLinejoin="round"
      strokeWidth="1.8"
    >
      {paths[name]}
    </svg>
  )
}

export function Detail({
  icon,
  label,
  value,
}: {
  icon?: IconName
  label: string
  value: string
}) {
  return (
    <div className="detail-row">
      {icon && (
        <span className="detail-icon">
          <Icon name={icon} />
        </span>
      )}
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  )
}

export function StatusBadge({
  label,
  status,
}: {
  label: string
  status: string
}) {
  return (
    <span className="status-badge" data-status={status}>
      <span aria-hidden="true" />
      {label}
    </span>
  )
}

export function EmptyState({
  description,
  icon = 'contract',
  title,
}: {
  description?: string
  icon?: IconName
  title: string
}) {
  return (
    <div className="empty-state">
      <span className="empty-state-icon">
        <Icon name={icon} />
      </span>
      <h2>{title}</h2>
      {description && <p>{description}</p>}
    </div>
  )
}

export function ErrorState({
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
      <Icon name="alert" />
      <div>
        <strong>{message}</strong>
        <button className="text-button" onClick={onRetry} type="button">
          {retryLabel}
        </button>
      </div>
    </div>
  )
}
