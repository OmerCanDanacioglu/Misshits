import { useRef, useCallback } from 'react'

const SENTENCE_ENDINGS = /[.!?,;\n]$/
const API_BASE = ''

export interface WordCorrection {
  original: string
  corrected: string
}

/**
 * Hook that detects sentence-ending punctuation and sends the sentence
 * to the backend for correction via SmartConnection LLM.
 * Only the auto-corrected words are flagged for review.
 *
 * onCorrected receives the original sentence and its correction so the
 * caller can replace based on the *current* text, not stale captured text.
 */
export function useSentenceCorrection(
  onCorrected: (originalSentence: string, correctedSentence: string) => void,
  onDone?: () => void
) {
  const pendingRef = useRef(false)

  const checkAndCorrect = useCallback(async (
    text: string,
    corrections: WordCorrection[]
  ) => {
    if (pendingRef.current) return
    if (!SENTENCE_ENDINGS.test(text)) return

    // Find the start of the last sentence/clause
    const textBeforePunc = text.slice(0, -1)
    const prevEnd = Math.max(
      textBeforePunc.lastIndexOf('.'),
      textBeforePunc.lastIndexOf('!'),
      textBeforePunc.lastIndexOf('?'),
      textBeforePunc.lastIndexOf(','),
      textBeforePunc.lastIndexOf(';'),
      textBeforePunc.lastIndexOf('\n')
    )

    const sentenceStart = prevEnd === -1 ? 0 : prevEnd + 1
    const sentence = text.slice(sentenceStart).trim()

    if (sentence.length < 3 || corrections.length === 0) {
      onDone?.()
      return
    }

    pendingRef.current = true

    try {
      const res = await fetch(`${API_BASE}/api/correct-sentence`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ sentence, corrections }),
      })

      if (!res.ok) {
        onDone?.()
        return
      }

      const data: { corrected: string } = await res.json()
      if (!data.corrected || data.corrected === sentence) {
        onDone?.()
        return
      }

      onCorrected(sentence, data.corrected)
    } catch {
      onDone?.()
    } finally {
      pendingRef.current = false
    }
  }, [onCorrected, onDone])

  return checkAndCorrect
}
