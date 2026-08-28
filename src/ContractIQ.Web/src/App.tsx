import { useState } from 'react'

type Language = 'en' | 'pt-BR'

const content = {
  en: {
    languageLabel: 'Language',
    eyebrow: 'Contract intelligence, grounded in evidence',
    title: 'Understand contracts. Act with confidence.',
    description:
      'ContractIQ brings customer data, contract clauses, and internal policies together to answer questions with clear references.',
    statusTitle: 'The workspace is being prepared',
    statusDescription:
      'Contract search, cancellation assessment, and guided actions will appear here as the project evolves.',
    principleTitle: 'Built on a simple principle',
    principleDescription:
      'AI helps people understand and choose an action. Business rules remain deterministic and auditable.',
  },
  'pt-BR': {
    languageLabel: 'Idioma',
    eyebrow: 'Inteligência contratual baseada em evidências',
    title: 'Entenda contratos. Decida com confiança.',
    description:
      'O ContractIQ reúne dados de clientes, cláusulas contratuais e políticas internas para responder perguntas com referências claras.',
    statusTitle: 'O ambiente está sendo preparado',
    statusDescription:
      'Busca em contratos, análise de cancelamento e ações guiadas aparecerão aqui conforme o projeto evolui.',
    principleTitle: 'Construído sobre um princípio simples',
    principleDescription:
      'A IA ajuda pessoas a entender e escolher uma ação. As regras de negócio continuam determinísticas e auditáveis.',
  },
} satisfies Record<Language, Record<string, string>>

export function App() {
  const [language, setLanguage] = useState<Language>('en')
  const copy = content[language]

  function changeLanguage(nextLanguage: Language) {
    setLanguage(nextLanguage)
    document.documentElement.lang = nextLanguage
  }

  return (
    <div className="app-shell">
      <header className="topbar">
        <a className="brand" href="#main-content" aria-label="ContractIQ home">
          <span className="brand-mark" aria-hidden="true">
            CI
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

      <main id="main-content">
        <section className="hero" aria-labelledby="hero-title">
          <div className="hero-copy">
            <p className="eyebrow">{copy.eyebrow}</p>
            <h1 id="hero-title">{copy.title}</h1>
            <p className="hero-description">{copy.description}</p>
          </div>

          <div className="status-card">
            <span className="status-indicator" aria-hidden="true" />
            <div>
              <h2>{copy.statusTitle}</h2>
              <p>{copy.statusDescription}</p>
            </div>
          </div>
        </section>

        <section className="principle-card" aria-labelledby="principle-title">
          <p className="principle-number" aria-hidden="true">
            01
          </p>
          <div>
            <h2 id="principle-title">{copy.principleTitle}</h2>
            <p>{copy.principleDescription}</p>
          </div>
        </section>
      </main>

      <footer>
        <span>ContractIQ</span>
        <span>DDD · CQRS · RAG</span>
      </footer>
    </div>
  )
}
