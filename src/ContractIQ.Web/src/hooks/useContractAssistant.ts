import { useRef, useState } from 'react'

import { ApiError, contractIqApi, type ContractAnswer } from '../api'
import { translations, type Language } from '../translations'

export function useContractAssistant({
  customerId,
  contractId,
  language,
  getErrorMessage,
}: {
  customerId?: string
  contractId?: string
  language: Language
  getErrorMessage: (error: unknown) => string
}) {
  const [question, setQuestion] = useState('')
  const [submittedQuestion, setSubmittedQuestion] = useState<string>()
  const [answer, setAnswer] = useState<ContractAnswer>()
  const [error, setError] = useState<string>()
  const [isLoading, setIsLoading] = useState(false)
  const requestSequence = useRef(0)

  function reset() {
    requestSequence.current += 1
    setQuestion('')
    setSubmittedQuestion(undefined)
    setAnswer(undefined)
    setError(undefined)
    setIsLoading(false)
  }

  function clearProposedAction() {
    setAnswer((current) =>
      current ? { ...current, proposedAction: null } : current,
    )
  }

  async function ask() {
    if (!customerId || !contractId || question.trim().length < 3) {
      return
    }

    const normalizedQuestion = question.trim()
    setIsLoading(true)
    setSubmittedQuestion(normalizedQuestion)
    setAnswer(undefined)
    setError(undefined)
    const requestId = requestSequence.current + 1
    requestSequence.current = requestId

    try {
      const response = await contractIqApi.askContractQuestion(
        normalizedQuestion,
        customerId,
        contractId,
        language,
      )
      if (requestSequence.current === requestId) {
        setAnswer(response)
      }
    } catch (requestError) {
      if (requestSequence.current === requestId) {
        setError(
          requestError instanceof ApiError && requestError.status === 503
            ? translations[language].assistantUnavailable
            : getErrorMessage(requestError),
        )
      }
    } finally {
      if (requestSequence.current === requestId) {
        setIsLoading(false)
      }
    }
  }

  return {
    answer,
    ask,
    clearProposedAction,
    error,
    isLoading,
    question,
    reset,
    setQuestion,
    submittedQuestion,
  }
}
