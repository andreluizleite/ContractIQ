import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'

import { App } from './App'

describe('App', () => {
  it('shows the English experience by default', () => {
    render(<App />)

    expect(
      screen.getByRole('heading', {
        name: 'Understand contracts. Act with confidence.',
      }),
    ).toBeInTheDocument()
  })

  it('switches the interface to Brazilian Portuguese', async () => {
    const user = userEvent.setup()
    render(<App />)

    await user.selectOptions(screen.getByRole('combobox'), 'pt-BR')

    expect(
      screen.getByRole('heading', {
        name: 'Entenda contratos. Decida com confiança.',
      }),
    ).toBeInTheDocument()
    expect(document.documentElement.lang).toBe('pt-BR')
  })
})
