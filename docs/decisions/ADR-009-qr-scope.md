# ADR-009: QR scope in v1
Status: Accepted · Date: 2026-10-20

## Decision
- QR reading works on **image files only** (`image.read-qr`). Result text goes to the clipboard and a notification shows the first 80 characters.
- Reading QR codes from PDF pages is out of scope for v1: it would make the QR module depend on the PDF renderer. Revisit by adding a `pdf.read-qr` target implemented in WP-10 using `IPdfPageRenderer` (contract change → new ADR).
- If several codes are found, all are returned, separated by new lines.
- **Never open a decoded URL automatically.** Clipboard only (decoded text is untrusted).
