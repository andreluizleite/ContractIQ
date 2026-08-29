import { useEffect, useMemo, useState } from 'react'

import {
  contractIqApi,
  type CancellationAssessment,
  type ContractDetails,
  type ContractSummary,
  type CustomerSummary,
} from '../api'

export type Loadable<T> =
  | { status: 'loading' }
  | { status: 'error'; error: unknown }
  | { status: 'ready'; data: T }

export type ContractWorkspace = {
  details: ContractDetails
  assessment: CancellationAssessment
}

const initialLoad = { status: 'loading' } as const

export function useContractWorkspace() {
  const [customers, setCustomers] =
    useState<Loadable<CustomerSummary[]>>(initialLoad)
  const [customersReload, setCustomersReload] = useState(0)
  const [selectedCustomerId, setSelectedCustomerId] = useState<string>()
  const [contracts, setContracts] = useState<Loadable<
    ContractSummary[]
  > | null>(null)
  const [contractsReload, setContractsReload] = useState(0)
  const [selectedContractId, setSelectedContractId] = useState<string>()
  const [workspace, setWorkspace] =
    useState<Loadable<ContractWorkspace> | null>(null)
  const [workspaceReload, setWorkspaceReload] = useState(0)

  const selectedCustomer = useMemo(
    () =>
      customers.status === 'ready'
        ? customers.data.find((customer) => customer.id === selectedCustomerId)
        : undefined,
    [customers, selectedCustomerId],
  )

  useEffect(() => {
    const controller = new AbortController()

    contractIqApi
      .listCustomers(controller.signal)
      .then((data) => setCustomers({ status: 'ready', data }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setCustomers({ status: 'error', error })
        }
      })

    return () => controller.abort()
  }, [customersReload])

  useEffect(() => {
    if (!selectedCustomerId) {
      return
    }

    const controller = new AbortController()

    contractIqApi
      .listCustomerContracts(selectedCustomerId, controller.signal)
      .then((data) => setContracts({ status: 'ready', data }))
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setContracts({ status: 'error', error })
        }
      })

    return () => controller.abort()
  }, [selectedCustomerId, contractsReload])

  useEffect(() => {
    if (!selectedContractId) {
      return
    }

    const controller = new AbortController()

    Promise.all([
      contractIqApi.getContract(selectedContractId, controller.signal),
      contractIqApi.assessCancellation(selectedContractId, controller.signal),
    ])
      .then(([details, assessment]) =>
        setWorkspace({ status: 'ready', data: { details, assessment } }),
      )
      .catch((error: unknown) => {
        if (!controller.signal.aborted) {
          setWorkspace({ status: 'error', error })
        }
      })

    return () => controller.abort()
  }, [selectedContractId, workspaceReload])

  function selectCustomer(customerId: string) {
    setSelectedCustomerId(customerId)
    setContracts(initialLoad)
    setSelectedContractId(undefined)
    setWorkspace(null)
  }

  function selectContract(contractId: string) {
    setSelectedContractId(contractId)
    setWorkspace(initialLoad)
  }

  function retryCustomers() {
    setCustomers(initialLoad)
    setCustomersReload((value) => value + 1)
  }

  function retryContracts() {
    setContracts(initialLoad)
    setContractsReload((value) => value + 1)
  }

  function retryWorkspace() {
    setWorkspace(initialLoad)
    setWorkspaceReload((value) => value + 1)
  }

  return {
    contracts,
    customers,
    retryContracts,
    retryCustomers,
    retryWorkspace,
    selectContract,
    selectCustomer,
    selectedContractId,
    selectedCustomer,
    selectedCustomerId,
    workspace,
  }
}
