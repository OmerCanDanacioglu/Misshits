import { useState, useEffect, useCallback, useRef } from 'react'
import { useSpellCheck, fetchSpellSuggestions } from '../lib/useSpellCheck'
import { useSentenceCorrection } from '../lib/useSentenceCorrection'
import { useTextHistory } from '../lib/useTextHistory'
import { useWordPrediction } from '../lib/useWordPrediction'
import { useQuickPhrases } from '../lib/useQuickPhrases'
import './Keyboard.css'

interface KeyDef {
  label: string
  shiftLabel?: string
  code: string
  width?: number
  special?: boolean
  subLabel?: string
}

const ROWS: KeyDef[][] = [
  // Row 1: QWERTY + Backspace
  [
    { label: 'Q', code: 'KeyQ' },
    { label: 'W', code: 'KeyW' },
    { label: 'E', code: 'KeyE' },
    { label: 'R', code: 'KeyR' },
    { label: 'T', code: 'KeyT' },
    { label: 'Y', code: 'KeyY' },
    { label: 'U', code: 'KeyU' },
    { label: 'I', code: 'KeyI' },
    { label: 'O', code: 'KeyO' },
    { label: 'P', code: 'KeyP' },
    { label: '\u2190', code: 'Backspace', width: 1.5, special: true, subLabel: 'backspace' },
  ],
  // Row 2: Home row + Enter
  [
    { label: 'A', code: 'KeyA' },
    { label: 'S', code: 'KeyS' },
    { label: 'D', code: 'KeyD' },
    { label: 'F', code: 'KeyF' },
    { label: 'G', code: 'KeyG' },
    { label: 'H', code: 'KeyH' },
    { label: 'J', code: 'KeyJ' },
    { label: 'K', code: 'KeyK' },
    { label: 'L', code: 'KeyL' },
    { label: '\u21B5', code: 'Enter', width: 1.5, special: true, subLabel: 'enter' },
  ],
  // Row 3: Shift + ZXCV + punctuation + delete word
  [
    { label: '\u21E7', code: 'ShiftLeft', width: 1.2, special: true },
    { label: 'Z', code: 'KeyZ' },
    { label: 'X', code: 'KeyX' },
    { label: 'C', code: 'KeyC' },
    { label: 'V', code: 'KeyV' },
    { label: 'B', code: 'KeyB' },
    { label: 'N', code: 'KeyN' },
    { label: 'M', code: 'KeyM' },
    { label: ',', code: 'Comma' },
    { label: '.', code: 'Period' },
    { label: '\u2190', code: 'Backspace', width: 1.5, special: true, subLabel: 'delete word' },
  ],
  // Row 4: Bottom row
  [
    { label: '123', code: 'CapsLock', width: 1.2, special: true },
    { label: '!', code: 'Digit1', special: true },
    { label: '?', code: 'Slash', special: true },
    { label: 'space', code: 'Space', width: 8 },
    { label: 'Ctrl', code: 'ControlLeft', width: 1.2, special: true },
    { label: 'Alt', code: 'AltLeft', width: 1.2, special: true },
  ],
]

// Map key codes to the characters they produce
function keyToChar(code: string, shift: boolean, capsLock: boolean): string | null {
  // Find the key definition
  for (const row of ROWS) {
    for (const key of row) {
      if (key.code === code) {
        // Letter keys
        if (code.startsWith('Key')) {
          const letter = code.slice(3)
          const upper = shift !== capsLock // XOR: shift flips caps state
          return upper ? letter.toUpperCase() : letter.toLowerCase()
        }
        // Space
        if (code === 'Space') return ' '
        // Tab
        if (code === 'Tab') return '\t'
        // Keys with shift variants
        if (shift && key.shiftLabel) return key.shiftLabel
        if (key.label.length === 1) return key.label
        return null
      }
    }
  }
  return null
}

const MODIFIER_CODES = new Set([
  'ShiftLeft', 'ShiftRight',
  'ControlLeft', 'ControlRight',
  'AltLeft', 'AltRight',
])

function isShift(mods: Set<string>) {
  return mods.has('ShiftLeft') || mods.has('ShiftRight')
}

export default function Keyboard() {
  const [pressedKeys, setPressedKeys] = useState<Set<string>>(new Set())
  // Sticky modifiers toggled via mouse clicks
  const [stickyMods, setStickyMods] = useState<Set<string>>(new Set())
  const [text, setTextRaw] = useState('')
  const [capsLock, setCapsLock] = useState(false)
  const [autoCorrectEnabled, setAutoCorrectEnabled] = useState(true)
  const [shorterOnly, setShorterOnly] = useState(false)
  const [autoSpeak, setAutoSpeak] = useState(false)
  const [apiEnabled, setApiEnabled] = useState(true)
  const { currentWord, suggestions } = useSpellCheck(text, shorterOnly)
  const predictions = useWordPrediction(apiEnabled ? text : '')
  const { phrases, usePhrase, addPhrase, deletePhrase } = useQuickPhrases()
  const [phrasesOpen, setPhrasesOpen] = useState(false)
  const history = useTextHistory()

  // Wrap setText to push history before each change
  const setText = useCallback((action: string | ((prev: string) => string)) => {
    setTextRaw(prev => {
      const next = typeof action === 'function' ? action(prev) : action
      if (next !== prev) history.push(prev)
      return next
    })
  }, [history])

  const handleUndo = useCallback(() => {
    const prev = history.undo()
    if (prev !== null) setTextRaw(prev)
  }, [history])

  const handleClear = useCallback(() => {
    setText('')
  }, [setText])

  const handleSpeak = useCallback(() => {
    if (!text) return
    window.speechSynthesis.cancel()
    const utterance = new SpeechSynthesisUtterance(text)
    utterance.lang = 'en-GB'
    window.speechSynthesis.speak(utterance)
  }, [text])

  const insertPrediction = useCallback((word: string) => {
    setText(prev => {
      const needsSpace = prev.length > 0 && !prev.endsWith(' ') && !prev.endsWith('\n')
      return prev + (needsSpace ? ' ' : '') + word + ' '
    })
  }, [setText])

  const [correcting, setCorrecting] = useState(false)
  // Track words that were auto-corrected: {original: "helo", corrected: "hello"}
  const correctionsRef = useRef<Array<{original: string, corrected: string}>>([])
  const autoSpeakRef = useRef(autoSpeak)
  useEffect(() => { autoSpeakRef.current = autoSpeak }, [autoSpeak])

  const speakSentence = useCallback((fullText: string) => {
    // Extract and speak the last sentence
    const match = fullText.match(/[^.!?]*[.!?]\s*$/)
    if (!match) return
    const sentence = match[0].trim()
    if (!sentence) return
    window.speechSynthesis.cancel()
    const utterance = new SpeechSynthesisUtterance(sentence)
    utterance.lang = 'en-GB'
    window.speechSynthesis.speak(utterance)
  }, [])

  const handleDone = useCallback((spokeAlready?: boolean) => {
    setCorrecting(false)
    // If no corrections were sent but sentence ended, still auto-speak
    if (!spokeAlready && autoSpeakRef.current) {
      setTextRaw(current => {
        speakSentence(current)
        return current
      })
    }
    correctionsRef.current = []
  }, [speakSentence])
  const checkAndCorrect = useSentenceCorrection(
    useCallback((originalSentence: string, correctedSentence: string) => {
      // Replace based on current text, not stale captured text
      setText(prev => {
        const idx = prev.lastIndexOf(originalSentence)
        if (idx === -1) return prev // Sentence no longer in text, skip
        return prev.slice(0, idx) + correctedSentence + prev.slice(idx + originalSentence.length)
      })
      if (autoSpeakRef.current) {
        window.speechSynthesis.cancel()
        const utterance = new SpeechSynthesisUtterance(correctedSentence)
        utterance.lang = 'en-GB'
        window.speechSynthesis.speak(utterance)
      }
      handleDone(true)
    }, [handleDone, setText]),
    handleDone
  )

  // Trigger sentence correction on punctuation or newline
  const prevTextRef = useRef(text)
  useEffect(() => {
    if (apiEnabled && text.length > prevTextRef.current.length && /[.!?,;\n]$/.test(text)) {
      setCorrecting(true)
      checkAndCorrect(text, correctionsRef.current)
    }
    prevTextRef.current = text
  }, [text, checkAndCorrect, apiEnabled])

  // Keep refs so callbacks can read latest values without re-creating
  const suggestionsRef = useRef(suggestions)
  useEffect(() => {
    suggestionsRef.current = suggestions
  }, [suggestions])

  const currentWordRef = useRef(currentWord)
  useEffect(() => {
    currentWordRef.current = currentWord
  }, [currentWord])

  const autoCorrectRef = useRef(autoCorrectEnabled)
  useEffect(() => {
    autoCorrectRef.current = autoCorrectEnabled
  }, [autoCorrectEnabled])

  // Fire a direct API call to correct a word that was typed too fast for suggestions
  const performDeferredCorrection = useCallback(async (word: string, suffix: string) => {
    try {
      const results = await fetchSpellSuggestions(word)
      if (results.length === 0 || results[0].distance === 0) return

      const corrected = results[0].term
      setText(prev => {
        const pattern = word + suffix
        const idx = prev.lastIndexOf(pattern)
        if (idx === -1) return prev
        correctionsRef.current.push({ original: word, corrected })
        return prev.slice(0, idx) + corrected + suffix + prev.slice(idx + pattern.length)
      })
    } catch {
      // Network error — silently drop
    }
  }, [setText])

  // Auto-correct the last word and append the given suffix
  const autoCorrectAndAppend = useCallback((suffix: string) => {
    setText(prev => {
      const match = prev.match(/[a-zA-Z]+$/)
      if (!match || !autoCorrectRef.current) {
        return prev + suffix
      }

      const word = match[0]
      const sug = suggestionsRef.current

      // Only use cached suggestions if they're for THIS word (not a stale partial)
      const suggestionsAreForCurrentWord =
        currentWordRef.current.toLowerCase() === word.toLowerCase()

      if (suggestionsAreForCurrentWord && sug.length > 0 && sug[0].distance > 0) {
        const corrected = sug[0].term
        correctionsRef.current.push({ original: word, corrected })
        return prev.slice(0, match.index!) + corrected + suffix
      }

      // Suggestions are stale or empty — fire a direct API call for deferred correction
      if (word.length >= 2) {
        performDeferredCorrection(word, suffix)
      }
      return prev + suffix
    })
  }, [setText, performDeferredCorrection])

  const autoCorrectAndAddSpace = useCallback(() => {
    autoCorrectAndAppend(' ')
  }, [autoCorrectAndAppend])

  const applySuggestion = useCallback((term: string) => {
    setText(prev => {
      const match = prev.match(/[a-zA-Z]+$/)
      if (!match) return prev
      correctionsRef.current.push({ original: match[0], corrected: term })
      return prev.slice(0, match.index!) + term
    })
  }, [])

  // Clear non-CapsLock sticky modifiers after a character key is used
  const clearStickyMods = useCallback(() => {
    setStickyMods(new Set())
  }, [])

  const handleKeyDown = useCallback((e: KeyboardEvent) => {
    e.preventDefault()
    setPressedKeys(prev => {
      const next = new Set(prev)
      next.add(e.code)
      return next
    })

    if (e.code === 'CapsLock') {
      setCapsLock(prev => !prev)
      return
    }
    if (MODIFIER_CODES.has(e.code)) return

    if (e.code === 'Backspace') {
      if (e.ctrlKey) {
        setText(prev => {
          // Delete trailing whitespace, then the word before it
          const trimmed = prev.replace(/\s+$/, '')
          const lastWord = trimmed.search(/\S+$/)
          return lastWord === -1 ? '' : prev.slice(0, lastWord)
        })
      } else {
        setText(prev => prev.slice(0, -1))
      }
      return
    }
    if (e.code === 'Enter') {
      setText(prev => prev + '\n')
      return
    }
    if (e.code === 'Space') {
      autoCorrectAndAddSpace()
      clearStickyMods()
      return
    }

    const char = keyToChar(e.code, e.shiftKey, capsLock)
    if (char) {
      if ('.!?,;:\'"()-'.includes(char)) {
        autoCorrectAndAppend(char)
      } else {
        setText(prev => prev + char)
      }
    }
    // Clear sticky mods after typing a character via physical keyboard too
    clearStickyMods()
  }, [capsLock, clearStickyMods, autoCorrectAndAddSpace, autoCorrectAndAppend])

  const handleKeyUp = useCallback((e: KeyboardEvent) => {
    e.preventDefault()
    setPressedKeys(prev => {
      const next = new Set(prev)
      next.delete(e.code)
      return next
    })
  }, [])

  useEffect(() => {
    window.addEventListener('keydown', handleKeyDown)
    window.addEventListener('keyup', handleKeyUp)
    return () => {
      window.removeEventListener('keydown', handleKeyDown)
      window.removeEventListener('keyup', handleKeyUp)
    }
  }, [handleKeyDown, handleKeyUp])

  const handleMouseClick = useCallback((code: string) => {
    // CapsLock: pure toggle
    if (code === 'CapsLock') {
      setCapsLock(prev => !prev)
      return
    }

    // Modifier keys: toggle sticky state
    if (MODIFIER_CODES.has(code)) {
      setStickyMods(prev => {
        const next = new Set(prev)
        if (next.has(code)) {
          next.delete(code)
        } else {
          next.add(code)
        }
        return next
      })
      return
    }

    // Action keys
    if (code === 'Backspace') {
      setText(prev => prev.slice(0, -1))
      clearStickyMods()
      return
    }
    if (code === 'Enter') {
      setText(prev => prev + '\n')
      clearStickyMods()
      return
    }

    if (code === 'Space') {
      autoCorrectAndAddSpace()
      clearStickyMods()
      return
    }

    // Character keys: use sticky modifier state
    const shift = isShift(stickyMods)
    const char = keyToChar(code, shift, capsLock)
    if (char) {
      if ('.!?,;:\'"()-'.includes(char)) {
        autoCorrectAndAppend(char)
      } else {
        setText(prev => prev + char)
      }
    }
    clearStickyMods()
  }, [stickyMods, capsLock, clearStickyMods, autoCorrectAndAddSpace, autoCorrectAndAppend])

  return (
    <div className="keyboard-wrapper">
      <div className="toolbar">
        <div className="toolbar-actions">
          <button className="action-btn speak-btn" onMouseDown={(e) => { e.preventDefault(); handleSpeak() }}>
            Speak
          </button>
          <button className="action-btn clear-btn" onMouseDown={(e) => { e.preventDefault(); handleClear() }}>
            Clear
          </button>
          <button className="action-btn undo-btn" onMouseDown={(e) => { e.preventDefault(); handleUndo() }}>
            Undo
          </button>
          <button
            className={`action-btn phrases-btn${phrasesOpen ? ' active' : ''}`}
            onMouseDown={(e) => { e.preventDefault(); setPhrasesOpen(p => !p) }}
          >
            Phrases
          </button>
        </div>
        <div className="toolbar-toggles">
          <label className="toggle">
            <input
              type="checkbox"
              checked={autoCorrectEnabled}
              onChange={(e) => setAutoCorrectEnabled(e.target.checked)}
            />
            <span className="toggle-slider" />
            <span className="toggle-label">Auto-correct</span>
          </label>
          <label className="toggle">
            <input
              type="checkbox"
              checked={shorterOnly}
              onChange={(e) => setShorterOnly(e.target.checked)}
            />
            <span className="toggle-slider" />
            <span className="toggle-label">Shorter/equal only</span>
          </label>
          <label className="toggle">
            <input
              type="checkbox"
              checked={autoSpeak}
              onChange={(e) => setAutoSpeak(e.target.checked)}
            />
            <span className="toggle-slider" />
            <span className="toggle-label">Auto-speak</span>
          </label>
          <label className="toggle">
            <input
              type="checkbox"
              checked={apiEnabled}
              onChange={(e) => setApiEnabled(e.target.checked)}
            />
            <span className="toggle-slider" />
            <span className="toggle-label">Smart API</span>
          </label>
        </div>
      </div>
      {phrasesOpen && (
        <div className="phrases-panel">
          <div className="phrases-grid">
            {phrases.map(p => (
              <button
                key={p.id}
                className="phrase-btn"
                onMouseDown={async (e) => {
                  e.preventDefault()
                  const phraseText = await usePhrase(p.id)
                  if (phraseText) {
                    setText(prev => {
                      const needsSpace = prev.length > 0 && !prev.endsWith(' ') && !prev.endsWith('\n')
                      return prev + (needsSpace ? ' ' : '') + phraseText
                    })
                  }
                }}
              >
                <span className="phrase-text">{p.text}</span>
                <span
                  className="phrase-delete"
                  onMouseDown={(e) => {
                    e.stopPropagation()
                    e.preventDefault()
                    deletePhrase(p.id)
                  }}
                >x</span>
              </button>
            ))}
          </div>
          <div className="phrases-add">
            <button
              className="action-btn"
              onMouseDown={(e) => {
                e.preventDefault()
                // Save current text as a quick phrase
                const trimmed = text.trim()
                if (trimmed) addPhrase(trimmed)
              }}
            >
              Save current text as phrase
            </button>
          </div>
        </div>
      )}
      <div className="text-output">
        <pre>{text}<span className="cursor">|</span></pre>
        {correcting && <span className="correcting-indicator">Correcting...</span>}
      </div>
      {suggestions.length > 0 ? (
        <div className="suggestions-bar">
          <span className="suggestions-label">{currentWord}:</span>
          {suggestions.map(s => (
            <button
              key={s.term}
              className="suggestion"
              onMouseDown={(e) => {
                e.preventDefault()
                applySuggestion(s.term)
              }}
            >
              {s.term}
            </button>
          ))}
        </div>
      ) : predictions.length > 0 ? (
        <div className="predictions-bar">
          {predictions.map(word => (
            <button
              key={word}
              className="prediction"
              onMouseDown={(e) => {
                e.preventDefault()
                insertPrediction(word)
              }}
            >
              {word}
            </button>
          ))}
        </div>
      ) : (
        <div className="predictions-bar empty" />
      )}
      <div className="keyboard">
        {ROWS.map((row, rowIndex) => (
        <div className="keyboard-row" key={rowIndex}>
          {row.map(key => {
            const isPhysical = pressedKeys.has(key.code)
            const isSticky = stickyMods.has(key.code)
            const isCapsActive = key.code === 'CapsLock' && capsLock
            const isActive = isPhysical || isSticky || isCapsActive
            const style = key.width
              ? { flex: `${key.width} 1 0` }
              : undefined
            const classNames = [
              'key',
              isActive ? 'pressed' : '',
              isSticky || isCapsActive ? 'toggled' : '',
              key.code === 'Space' ? 'space' : '',
              key.special ? 'special' : '',
            ].filter(Boolean).join(' ')

            // Determine displayed label based on shift/caps state
            const shiftActive = isShift(stickyMods) || pressedKeys.has('ShiftLeft') || pressedKeys.has('ShiftRight')
            const isLetter = key.code.startsWith('Key') && !key.special
            let displayLabel = key.label
            if (isLetter) {
              const upper = shiftActive !== capsLock
              displayLabel = upper ? key.label.toUpperCase() : key.label.toLowerCase()
            }

            return (
              <button
                key={key.code + key.subLabel}
                className={classNames}
                style={style}
                onMouseDown={(e) => {
                  e.preventDefault()
                  handleMouseClick(key.code)
                }}
              >
                <span className="main-label">{displayLabel}</span>
                {key.subLabel && (
                  <span className="sub-label">{key.subLabel}</span>
                )}
              </button>
            )
          })}
        </div>
        ))}
      </div>
    </div>
  )
}
