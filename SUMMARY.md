# Misshits - Virtual Keyboard with Spell Correction

## Overview

A web application featuring an interactive UK QWERTY virtual keyboard with real-time spell correction powered by the SymSpell algorithm and the SUBTLEX-UK frequency corpus.

## Architecture

### Frontend (Vite + React + TypeScript)

- **Virtual Keyboard** (`src/components/Keyboard.tsx`) - Full UK QWERTY layout rendered as clickable buttons. Responds to both physical keyboard input and mouse clicks.
- **Spell Check Hook** (`src/lib/useSpellCheck.ts`) - React hook that extracts the last word being typed and queries the backend API with 150ms debounce and request cancellation.
- **Vite Proxy** (`vite.config.ts`) - Proxies `/api` requests to the .NET backend at `localhost:5050`.

### Backend (.NET 8 Web API + SQLite)

- **SymSpell Service** (`api/Services/SymSpellService.cs`) - C# implementation of the SymSpell symmetric delete spell correction algorithm. Uses Damerau-Levenshtein distance (max edit distance 2) supporting insertions, deletions, replacements, and transpositions. Loads the full dictionary into memory at startup for fast lookups.
- **SQLite Database** (`api/Data/AppDbContext.cs`) - EF Core with SQLite storing the word frequency dictionary. Seeded on first run from the SUBTLEX-UK data file.
- **Database Seeder** (`api/Data/DatabaseSeeder.cs`) - Parses the SUBTLEX-UK.txt file and seeds ~66K valid English words (filtered by the `Spell_check` column to exclude misspellings from the subtitle corpus).
- **API Endpoint** - `GET /api/spellcheck?word=<word>&maxDistance=<n>&maxLength=<n>` returns up to 5 ranked suggestions.

## Features

### Keyboard
- Full UK QWERTY layout with correct key placements (including UK-specific keys like `£`, `#~` next to Enter, `\|` next to left Shift)
- Physical keyboard detection - keys highlight when pressed on real keyboard
- Mouse click support for all keys
- Modifier keys (Shift, Ctrl, Alt) are toggleable (sticky) when clicked - they stay active until a character is typed or they are clicked again
- Caps Lock toggles independently
- Key labels update dynamically based on Shift/Caps Lock state (letters change case, symbol keys swap to shifted variant)

### Spell Correction
- Real-time suggestions appear as the user types, shown in a bar between the text output and keyboard
- Top suggestion is visually highlighted
- Click any suggestion to replace the misspelled word
- **Auto-correct on Space** - when enabled, pressing Space automatically replaces the current word with the top suggestion
- Short word handling - 2-letter misspellings like "mi" are corrected to "me" when a much higher frequency alternative exists (10x+ frequency threshold)

### Toggles
- **Auto-correct** (on by default) - enables/disables automatic correction when Space is pressed
- **Shorter/equal only** (off by default) - restricts suggestions to words with the same or fewer letters than the misspelled input (filtered server-side via `maxLength` parameter)

### Text Output
- Text field above the keyboard showing typed output with a blinking cursor
- Supports newlines (Enter), backspace, and all printable characters

## Running the Application

### Backend
```bash
cd api
dotnet run --urls "http://localhost:5050"
```
On first run, the database is seeded from `api/Data/SUBTLEX-UK.txt` (takes ~30 seconds).

### Frontend
```bash
npm install
npm run dev
```
Opens at `http://localhost:5173`.

## User Interventions & Course Corrections

The following points required manual intervention to redirect or fix the AI-generated implementation:

1. **Mouse clicks not producing text output** - The initial keyboard implementation only highlighted keys visually on mouse click but did not generate any text in the output field. The `onMouseDown` handlers were missing the text generation logic that the physical keyboard handlers had. User had to report that "keys don't work" before this was addressed.

2. **Modifier keys not toggleable** - Shift, Ctrl, and Alt keys initially behaved like regular keys (highlight on press, release on mouse up). User requested they be made properly toggleable so they could be clicked once to activate, used with another key, and auto-released — like a phone keyboard's sticky modifier behaviour.

3. **Key label state not updating** - Key labels were static regardless of Shift/Caps Lock state. User requested that letters visually change between uppercase and lowercase, and symbol keys swap their labels, to reflect the current modifier state.

4. **Architecture change: client-side to server-side** - The initial SymSpell implementation and frequency dictionary were entirely client-side (embedded TypeScript). When a real frequency dictionary (SUBTLEX-UK, 160K entries) was introduced, user redirected the approach: instead of trimming the dictionary to fit in the browser bundle, they requested a .NET backend with a proper database (SQLite) to serve the spell checking API.

5. **Dictionary contained misspellings** - The SUBTLEX-UK corpus is sourced from subtitles and contains misspelled words (93K entries marked "X" in the Spell_check column). Initial seeding included all words, causing the spell checker to consider typos like "helo" and "becuase" as valid. The `Spell_check` column had to be used to filter to only valid English words (UK, UKUS, US).

6. **2-letter word correction failing** - The sentence "help mi please" was not corrected to "help me please" because "mi" existed in the dictionary as a valid word (musical note). The SymSpell exact-match early return had to be modified: for short words (2 letters or fewer), if a distance-1 alternative has 10x+ higher frequency than the exact match, it is promoted as the top suggestion.

7. **Client-side filtering moved to backend** - The "shorter/equal only" toggle initially filtered suggestions in the frontend React hook. User specified that filtering should happen on the backend, not the UI. A `maxLength` query parameter was added to the API endpoint, and the filtering logic was moved into the SymSpell service's `Lookup` method.

## Data Source

Word frequencies come from [SUBTLEX-UK](https://psychology.nottingham.ac.uk/subtlex-uk/), a corpus of British English subtitle frequencies from the University of Nottingham. Only words marked as valid English (Spell_check column = UK, UKUS, or US) are included.
