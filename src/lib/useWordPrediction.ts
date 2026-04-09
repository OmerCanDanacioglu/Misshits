import { useState, useEffect, useRef } from 'react'

const API_BASE = ''

/**
 * Hook that fetches next-word predictions based on the current text context.
 * Triggers after a space (user finished a word) with debounce.
 */
export function useWordPrediction(text: string): string[] {
  const [predictions, setPredictions] = useState<string[]>([])
  const abortRef = useRef<AbortController | null>(null)

  useEffect(() => {
    // Only predict after a space or when text is empty (start of input)
    if (text.length > 0 && !text.endsWith(' ')) {
      return
    }

    const context = text.trim()
    if (context.length === 0) {
      setPredictions([])
      return
    }

    abortRef.current?.abort()
    const controller = new AbortController()
    abortRef.current = controller

    const timeoutId = setTimeout(async () => {
      try {
        const res = await fetch(`${API_BASE}/api/predict-words`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ context }),
          signal: controller.signal,
        })

        if (!res.ok) return

        const data: { predictions: string[] } = await res.json()
        setPredictions(data.predictions ?? [])
      } catch {
        // Aborted or network error
      }
    }, 300)

    return () => {
      clearTimeout(timeoutId)
      controller.abort()
    }
  }, [text])

  return predictions
}
