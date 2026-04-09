import { useRef, useCallback } from 'react'

const MAX_HISTORY = 50

/**
 * Tracks text state history for undo support.
 * Call `push` before each text change to save the current state.
 * Call `undo` to pop the last state.
 */
export function useTextHistory() {
  const stackRef = useRef<string[]>([])

  const push = useCallback((text: string) => {
    stackRef.current.push(text)
    if (stackRef.current.length > MAX_HISTORY) {
      stackRef.current.shift()
    }
  }, [])

  const undo = useCallback((): string | null => {
    if (stackRef.current.length === 0) return null
    return stackRef.current.pop()!
  }, [])

  const canUndo = useCallback(() => stackRef.current.length > 0, [])

  return { push, undo, canUndo }
}
