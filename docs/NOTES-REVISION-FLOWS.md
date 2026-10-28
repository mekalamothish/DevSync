# Study notes and revision

Phase-5 implementation rules. Verification results are recorded in [the phase tracker](PHASES.md).

## Personal notes

Notes and notebooks belong to the authenticated Member. List, search, filters, previews, edits, archive/restore, and revision cards enforce that ownership. Personal notes remain private; explicit sharing creates an independent copy in a [team workspace](TEAM-FLOWS.md). Revision schedules remain personal.

The note document is an ordered collection of typed blocks: paragraph, heading, code, checklist, article link, and Tiptap rich text. See [rich editors](RICH-EDITORS.md) for formatting and storage. A note-wide version guards content and ordering changes, including archive/restore. Stale requests return 409 and retain the submitted version so a later retry cannot overwrite another tab's work. Archive retains the note and its revision history; restore makes it active again.

Code and ordinary text are HTML encoded. Personal note code blocks have language-aware syntax colors and a Format code control; see [code editor usage and boundaries](NOTE-CODE-EDITOR.md). Article links may point to published content or the owner's private draft, with access checked when saving and viewing. A link never makes another author's draft accessible.

The editor supports manual saving and keyboard block controls. JavaScript adds local block movement and autosave. An in-flight save must finish before a manual submit, and changes typed while saving remain pending. Conflicts pause saving and tell the author to preserve their work before reloading. The saved preview represents persisted content.

## Revision rules

Each card contains a plain-text prompt and answer associated with a private note. Creating a card makes it immediately due. Archive suspends review eligibility without deleting cards or history. The dashboard uses real counts, including due and upcoming cards and completed reviews.

The baseline scheduler uses a simple deterministic interval rule:

| Grade | First successful interval | Later interval |
|---|---|---|
| Again | 10 minutes; reset the day interval | 10 minutes; reset the day interval |
| Hard | 1 day | Round up the previous day interval × 1.2, minimum 1 day |
| Good | 3 days | Round up the previous day interval × 2, minimum 3 days |
| Easy | 7 days | Round up the previous day interval × 3, minimum 7 days |

Day intervals are capped at 365 days. This is an application scheduling policy; it is not presented as SM-2 or a personalized retention model. Due times and review history use UTC. A clock service makes boundary behavior testable. A card is due when `DueAt <= now`; future reviews are rejected.

A grade submission includes the card version and a unique submission ID. Updating the schedule and recording history happen in one transaction. Retrying the same completed submission returns the existing result; a different stale or concurrent submission cannot advance the schedule twice. Ownership applies to both the source note and the card, and posted user identifiers do not determine access.

## Acceptance checks

- Notes persist, search/filter results respect ownership, and notebooks cannot be assigned across owners.
- Block order persists and stale/concurrent saves cannot overwrite the winning note.
- Private article links do not reveal other authors' drafts; rendered text/code are safe.
- Archive/restore preserve content/history and control revision eligibility.
- Grades produce deterministic UTC intervals and due counts at exact boundaries.
- Submission retries and simultaneous grades create at most one review for a card version.
- Manual block controls remain usable without JavaScript, and autosave preserves edits made during a save.
