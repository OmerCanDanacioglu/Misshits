import { useState, useEffect, useCallback } from 'react'

const API_BASE = ''

export interface QuickPhrase {
  id: number
  text: string
  usageCount: number
}

export function useQuickPhrases() {
  const [phrases, setPhrases] = useState<QuickPhrase[]>([])

  const fetchPhrases = useCallback(async () => {
    try {
      const res = await fetch(`${API_BASE}/api/phrases`)
      if (res.ok) {
        setPhrases(await res.json())
      }
    } catch { /* ignore */ }
  }, [])

  useEffect(() => { fetchPhrases() }, [fetchPhrases])

  const usePhrase = useCallback(async (id: number): Promise<string | null> => {
    const phrase = phrases.find(p => p.id === id)
    if (!phrase) return null

    try {
      await fetch(`${API_BASE}/api/phrases/${id}/use`, { method: 'POST' })
      // Update locally
      setPhrases(prev => prev.map(p =>
        p.id === id ? { ...p, usageCount: p.usageCount + 1 } : p
      ).sort((a, b) => b.usageCount - a.usageCount))
    } catch { /* ignore */ }

    return phrase.text
  }, [phrases])

  const addPhrase = useCallback(async (text: string) => {
    try {
      const res = await fetch(`${API_BASE}/api/phrases`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ text }),
      })
      if (res.ok) {
        await fetchPhrases()
      }
    } catch { /* ignore */ }
  }, [fetchPhrases])

  const deletePhrase = useCallback(async (id: number) => {
    try {
      await fetch(`${API_BASE}/api/phrases/${id}`, { method: 'DELETE' })
      setPhrases(prev => prev.filter(p => p.id !== id))
    } catch { /* ignore */ }
  }, [])

  return { phrases, usePhrase, addPhrase, deletePhrase }
}
