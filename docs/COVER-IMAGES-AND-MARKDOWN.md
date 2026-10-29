# Cover images and Markdown

Blog and study-note editors have an optional **Cover image** section near the top. Choose a PNG or JPEG up to 5 MB using **Upload or replace cover**. The server validates the actual image bytes and saves it privately in **My images**. The preview appears after upload succeeds. Save the document or wait for its autosave confirmation before leaving.

**Remove cover** clears the document’s selected thumbnail. Uploading another image replaces the selection. Both operations preserve the old uploaded file in the image library, so an image referenced elsewhere is not deleted. A failed upload keeps the previous cover selected. Covers share the existing personal-library 100-image quota.

Study notes and their cover endpoints are owner-only, including archived notes. Blog draft covers are owner-only. Publishing snapshots the selected cover together with the article. A later draft replacement/removal does not change the public card until the author publishes again. Public covers use a blog-specific endpoint; publishing does not enable the original image’s public-library link. Hidden articles do not serve a public cover.

The Explore, Saved articles, public profile, My blogs and Study Notes cards show selected landscape thumbnails. Uploaded dimensions are retained; cards crop to 16:9 with object-fit. Existing documents with no cover continue to work.

Markdown uses the BSD-licensed Markdig 1.5.0 parser with pipe tables, emphasis, strikethrough, headings, nested/numbered lists, quotes, task lists, horizontal rules and automatic heading IDs for table-of-contents anchors. Use standard Markdown, for example `**bold**` without spaces inside the markers. Raw HTML remains inactive; links and images pass the application’s URL policy. Existing Markdown bodies are not rewritten. Rich-text and block editors retain their separate content.

Registration validates password requirements before account/profile writes. Invalid submissions leave neither account nor handle behind. Repeating an unverified registration with the same email, handle and correct password resumes email verification without creating a duplicate or overwriting account details. Existing confirmed accounts still require sign-in; **Resend verification email** is available from the registration screen. Duplicate-submit prevention supplements server-side transactions.

## Validation

Regression tests cover invalid-password correction, pending-registration retry, Markdown structures and unsafe targets, authenticated uploads, cover reload/replacement/removal, foreign-owner rejection, public snapshot isolation, and note privacy. Client tests cover upload failures, pending submission, CSRF, oversized files, selection removal and duplicate registration submits.

Automated Chrome file selection requires the ChatGPT browser extension’s file-URL permission. That permission was unavailable during the UI check; real upload/save/replace/remove flows were verified with the HTTP/PostgreSQL tests, and the browser verified an existing sample cover’s rendering and saved removal.
