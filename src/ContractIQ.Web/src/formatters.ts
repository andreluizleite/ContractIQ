import type { Money } from './api'
import type { Language } from './translations'

export function formatDate(value: string, language: Language) {
  return new Intl.DateTimeFormat(language, {
    dateStyle: 'medium',
    timeZone: 'UTC',
  }).format(new Date(`${value}T00:00:00Z`))
}

export function formatMoney(money: Money, language: Language) {
  return new Intl.NumberFormat(language, {
    style: 'currency',
    currency: money.currency,
  }).format(money.amount)
}

export function shortId(id: string) {
  return id.slice(0, 8).toUpperCase()
}
