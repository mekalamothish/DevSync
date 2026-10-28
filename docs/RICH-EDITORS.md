# Blog and Study Notes editors

## Blogs

Create or edit an article and choose **Writing editor**:

- **Markdown** keeps the original Markdown editor and rendering rules.
- **Study Notes blocks** uses the same text, heading, code, checklist, linked-article and rich-text block components as personal notes. Add, remove and move blocks using their buttons. Code blocks retain Monaco colors, language selection, formatting and the plain-text fallback.
- **Rich text · Tiptap** edits the entire article with a visual toolbar.

Each editor keeps its own draft body. Switching editors selects which body appears in saved preview and publication; it does not convert, erase or merge the other bodies. Copy desired content into another editor when needed. Autosave and manual save include all draft bodies. Existing Markdown articles remain Markdown after migration.

Publishing stores a separate snapshot of the selected editor. Further draft edits and editor switches do not affect that copy until publishing again. Public readers never receive inactive draft bodies, and private linked articles are omitted from public rendering. Ownership, moderation and version checks remain enforced.

## Study Notes

Choose **Add a block → Rich text · Tiptap → Add block**. This adds a visual editor alongside existing block types. Rich text survives block movement, autosave, manual saving, reopening and saved preview. Archived notes are read-only. Existing notes and code editors are retained.

## Rich text toolbar

The toolbar supports paragraphs, headings 1–3, bold, italic, underline, strike, bullet and numbered lists, checklists, quotes, inline code, code blocks, horizontal rules, links, images, Undo and Redo. Link/image buttons open inline URL controls; Enter applies and Escape cancels. Images use HTTPS or image-library URLs. Same-origin uploaded-image URLs are normalized to local asset paths. Image access still follows personal/team ownership and public-link settings.

**Show source** reveals structured JSON for troubleshooting or a no-editor fallback. **Use visual editor** validates the source before returning. Invalid source remains in the field and cannot overwrite the saved document. Switching to source closes pending URL controls and disables the hidden visual surface.

Rich text is stored as Tiptap JSON. The server validates node types, marks, nesting, URLs, size and depth, then emits encoded HTML from an allowlist. Script/event-handler HTML and unsupported nodes are never executed. Notes retain the 50-block/60,000-character aggregate limit; formatting counts toward it. Blogs allow 50 blocks and 60,000 content characters per editor. Team bodies retain their 50,000-character limit; oversized copies are rejected without truncation.

Rich articles and mixed rich notes copied to a team keep a rich JSON body and use the same Tiptap component for team editing. Private linked-article blocks are omitted under the existing sharing policy. Copying never makes private source images public.

## Dropdowns and theme

Select fields have consistent spacing, emerald chevrons, focus rings and disabled states. Supporting desktop browsers get rounded themed popup menus with hover/selected states. Other browsers and mobile devices retain native accessible pickers. Labels, keyboard/type-ahead and form values remain native HTML. Toolbars wrap on narrow screens; fieldsets shrink around Monaco and Tiptap to avoid horizontal overflow.

## Packages, build and storage

Only free, MIT-licensed Tiptap 3.32.0 packages are installed: core, ProseMirror, StarterKit, Image, TaskList and TaskItem, with their open-source dependencies. No Tiptap Cloud account, paid extension, subscription or runtime CDN is needed. See the [official open-source editor overview](https://tiptap.dev/docs/editor/getting-started/overview).

`npm ci` and `npm run css:build` bundle `rich-text.js` with the code editor and Tailwind styles. Docker/CI use the same build. The Tiptap license is copied into generated assets. The existing DOMPurify override was patched to 3.4.17; the dependency audit reports zero vulnerabilities.

The additive RichTextEditors and TeamRichTextCopies migrations retain old bodies and initialize format metadata to Markdown. A local pre-change database backup is retained in ignored `artifacts/pre-rich-editors.dump` with owner-only permissions. Restore from a backup when rolling back to an application that cannot understand the new formats; removing the schema columns would remove format-specific bodies.
