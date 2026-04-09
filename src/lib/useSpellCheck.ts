import { useState, useEffect, useRef } from 'react'

export interface SuggestionResult {
  term: string
  distance: number
  frequency: number
}

export interface SpellCheckState {
  currentWord: string
  suggestions: SuggestionResult[]
}

const API_BASE = ''  // Uses Vite proxy in dev (/api → localhost:5050)

/**
 * Hook that calls the backend SymSpell API for spell-checking.
 * Extracts the last word being typed and fetches suggestions for it.
 * Debounces requests to avoid overwhelming the API while typing fast.
 */
export function useSpellCheck(text: string, shorterOnly = false): SpellCheckState {
  const [state, setState] = useState<SpellCheckState>({
    currentWord: '',
    suggestions: [],
  })
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    const match = text.match(/[a-zA-Z]+$/)
    const currentWord = match ? match[0] : ''

    if (currentWord.length < 2) {
      setState({ currentWord: '', suggestions: [] })
      return
    }

    // Abort previous in-flight request
    abortRef.current?.abort()
    const controller = new AbortController()
    abortRef.current = controller

    const timeoutId = setTimeout(async () => {
      try {
        const params = new URLSearchParams({ word: currentWord })
        if (shorterOnly) {
          params.set('maxLength', String(currentWord.length))
        }
        const res = await fetch(
          `${API_BASE}/api/spellcheck?${params}`,
          { signal: controller.signal }
        )
        if (!res.ok) return

        const results: SuggestionResult[] = await res.json()

        // If top result is exact match (distance 0), no correction needed
        if (results.length > 0 && results[0].distance === 0) {
          setState({ currentWord, suggestions: [] })
          return
        }

        setState({ currentWord, suggestions: results.slice(0, 5) })
      } catch {
        // Aborted or network error — ignore
      }
    }, 150) // 150ms debounce

    return () => {
      clearTimeout(timeoutId)
      controller.abort()
    }
  }, [text, shorterOnly])

  return state
}
