# Image libraries

Open **My images** in the sidebar for your personal library. Open **Teams → your team → Team images** for a shared library. Blog and team editors also link to their image libraries without replacing the current editor tab.

1. Choose a PNG or JPEG and enter a meaningful image description (used as alternative text).
2. Upload the image. Every upload starts private.
3. Preview it in the gallery or open the full image.
4. Use **Copy URL** for an absolute application URL, or **Copy Markdown** for a portable same-origin reference: `![Description](/media/images/IMAGE-ID)`.
5. Paste the reference into a blog or team Markdown editor, save, and open its preview.

A personal image requires its owner's signed-in session. A private team image requires current membership in that team. Platform administrator/moderator roles do not bypass those checks. Team viewers can view/copy; members, admins and owners can upload. Only a personal image's owner or a team owner/admin can enable its public link. Publishing an article does not publish its images automatically: enable the image's public link explicitly when anonymous readers need to see it.

Public links allow anyone with the URL to read the image. Disabling a public link restores authorization on subsequent requests; it cannot recall copies already downloaded. Access is checked on every image request, with `private, no-store` responses and no MIME sniffing. Removing a team member immediately removes access to private team images. Team image ownership remains with the team when an uploader leaves.

## Limits and storage

- PNG: complete chunk CRCs, non-interlaced structure and bounded decompressed pixel rows are validated.
- JPEG: baseline/progressive, 8-bit grayscale/RGB structure and dimensions are validated. JPEG entropy is not fully decoded or re-encoded on the server.
- Up to 5 MiB per file and 20 million pixels; 100 images per library. Concurrent uploads cannot bypass the quota. Gallery pages contain 24 metadata records and never query file bytes.
- SVG, GIF, WebP, HTML and arbitrary files are not supported. Filenames and browser MIME types do not determine the response type; original filenames are display metadata only.
- A 6 MiB multipart request limit leaves room for form fields. Standard antiforgery and authenticated write throttling apply.
- Files are PostgreSQL `bytea` records, with exactly one personal/team scope, dimension/content constraints, ownership foreign keys and versioned visibility changes. The existing database volume and backup workflow retain image data through Docker rebuilds. A full library can store up to 500 MiB; include images in capacity/backup planning.
- Image URLs use `Account:PublicBaseUrl`. Set it to the actual HTTPS origin in production. Copied relative Markdown remains valid when the application origin changes; trusted absolute image URLs render as same-origin paths. Private links copied into another person's article remain subject to the image's original permissions.

The implementation follows [ASP.NET Core's file upload guidance](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0) for bounded uploads, server-side file checks and untrusted filenames. Image deletion, editing, full JPEG decoding/re-encoding, external object storage, and malware scanning are not part of this release. Host-level upload scanning and representative storage/load testing remain deployment decisions.
