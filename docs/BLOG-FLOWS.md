# Blog workflow and acceptance

Phase 4 implements the author dashboard, technical editor, explore feed, and article reader from the screen map. Styling follows the observed shared shell and palette. Exact Figma tokens/assets and detailed visual parity remain under P1-05 and P3-06.

## Visibility and publication

- A confirmed Member (including Moderator/Administrator roles) can create and publish. Publisher verification is a separate phase 8 workflow.
- New articles are private drafts. The author alone can edit, autosave, or preview them. Other signed-in users receive 404 for those resources; anonymous users are challenged to sign in.
- Publishing atomically copies title, summary, Markdown, and normalized tags to a public snapshot. Later saves change the private working copy. Publishing again updates the public snapshot while keeping the original URL and first-publication date.
- Slugs contain the initial title stem plus the complete article ID. Renaming or duplicate titles cannot break or collide with existing URLs.
- Draft title is required, up to 160 characters. Summary is up to 500 characters, content 60,000 characters. Publishing requires summary and content. Tags allow at most five distinct lowercase names, each up to 30 letters/numbers/+, #, dot, or hyphen.
- Every save requires the last known concurrency version. Stale writes return 409, retain submitted work, and never refresh the submitted version automatically. The editor pauses on conflict and asks the author to copy work before reloading.
- Autosave runs after 2.5 seconds of inactivity, serializes requests, incorporates returned versions, and retains edits made during a request. Manual submission waits for an in-flight autosave. Unsaved changes trigger a browser leave warning. JavaScript is optional for manual saving and publishing.
- Preview shows the last saved draft in a separate tab. The editor explains that changes must be saved first.

## Content rendering

The editor uses a documented Markdown subset: headings (`#` through `###`), unordered lists, inline backticks, fenced code blocks, and absolute HTTP/HTTPS links. Every content fragment and link attribute is HTML encoded. Images use ![description](https://example.com/image.png), with encoded alt text and public HTTPS URLs without credentials, IP literals, or local host names. Images load directly in the browser with no referrer and resize within the article. Raw HTML is shown as text; script/data/protocol-relative URLs do not become links. No arbitrary HTML is accepted. Code is readable without syntax highlighting. This deliberately avoids a rich-text HTML storage/sanitization boundary.

## Discovery and reader actions

- Explore searches published titles/summaries, supports exact normalized tag filters, and pages at 12 articles with deterministic date/ID ordering. Draft title/tag changes never affect public search.
- My blogs pages the author's drafts and published working copies, reports real counts, and labels unpublished edits. Saved articles shows only the current user's bookmarks. Home displays up to three public articles and the signed-in author's counts.
- Likes and bookmarks set explicit on/off state and have unique `(post, user)` keys; repeated/concurrent requests cannot create duplicates. Only published articles support reader actions.
- Comments are plain text, limited to 2,000 characters, rendered with Razor encoding, and paginated at 20. Only their writer can delete them. Author/moderation privileges do not grant comment deletion here; moderation arrives in phase 8.
- All writes enforce authorization and antiforgery. Author/writer identity comes from the session, never a posted user ID. Personalized pages use no-store responses.

## Verification

Run `npm test`, `npm run db:test`, and `npm run test:client`. PostgreSQL tests cover draft privacy, public snapshot isolation, stable URLs, resource ownership, invalid publication, stale/simultaneous saves, filtering/pagination, idempotent reader actions, and encoded comments. Test users/content are cleaned up through foreign-key cascades in the dedicated `_tests` database.

For read-only visual review, run `DEVSYNC_CAPTURE_UI=1 npm run db:test` followed by `npm run preview:blogs`. Open `http://localhost:5181/reader.html`, `/editor.html`, `/dashboard.html`, or `/explore.html`. These are captured MVC responses with test tokens removed and submit buttons/autosave disabled; they do not keep test users or content in the development database. The review server binds to loopback only.

Remaining visual work: exact Figma comparison and bundled font/assets. Production throughput, search indexing beyond basic relational filters, and moderation are later phases.
