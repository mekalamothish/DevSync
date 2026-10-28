# Gmail delivery

DevSync uses the same email sender for registration verification, resend verification, password recovery and new team invitations. The default Development mode saves `.eml` files locally. Explicit `Smtp` mode enables real delivery while keeping the local app in Development.

## Required information

1. A Gmail sender address (or an eligible Google Workspace mailbox).
2. Google 2-Step Verification enabled on that account.
3. A Google App Password created for DevSync. Enter it only in the ignored local `.env` or your deployment secret store; do not paste it into chat or source control. Use the App Password without its display spaces, not your regular Google password.
4. The application's reachable URL. `http://localhost:5180` works for testing on this computer. People using other computers need your deployed public HTTPS origin; localhost in their email points to their own computer.

Google documents [App Password eligibility and creation](https://support.google.com/accounts/answer/185833). Some organization/Advanced Protection accounts do not offer App Passwords; those require a provider-supported authentication/relay arrangement instead. Gmail's documented SMTP server is `smtp.gmail.com`, with STARTTLS on port `587`: [Google SMTP settings](https://support.google.com/mail/answer/7104828). Receiving emails does not require enabling POP or IMAP.

## Docker setup

Add/update these entries in the existing `.env`, retaining all database entries:

```dotenv
EMAIL_DELIVERY=Smtp
EMAIL_FROM=your-sender@gmail.com
EMAIL_FROM_NAME=DevSync
EMAIL_SMTP_HOST=smtp.gmail.com
EMAIL_SMTP_PORT=587
EMAIL_SMTP_USERNAME=your-sender@gmail.com
EMAIL_SMTP_PASSWORD=your-google-app-password-without-spaces
ACCOUNT_PUBLIC_BASE_URL=http://localhost:5180
```

Use the same Gmail address for From and Username, unless you have configured an authorized alias. `EMAIL_FROM_NAME` defaults to `DevSync`; recipients see `DevSync <your-sender@gmail.com>`, and can still inspect the underlying address. Native runs use `Email__FromName` for this setting. Set the URL to your actual public HTTPS origin when testing with remote teammates. Updating a URL does not deploy the website or make localhost publicly reachable.

Apply the settings:

```sh
docker compose up -d --build --wait
```

For a native `dotnet` run, use environment variables `Email__Delivery`, `Email__From`, `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__Username`, `Email__Smtp__Password`, and `Account__PublicBaseUrl`, or .NET user secrets. Docker maps the uppercase `.env` names above into these .NET settings; a plain `dotnet run` does not automatically import `.env`.

## Verify delivery

- Register with a real email address, or use **Resend verification** for an unconfirmed account. Open the delivered link, confirm the address and sign in.
- Use **Forgot password** for a confirmed account. The public acknowledgement stays generic even when no account exists or delivery fails.
- As team Owner/Admin, open **Members and invitations**, enter the intended recipient and click **Invite teammate**. The email contains the team, role, expiry and acceptance link. The recipient must register/verify if needed, then reopen the invitation and sign in with the invited email address.
- SMTP submission success does not guarantee inbox placement; inspect Spam as well. A failed invitation email preserves the invitation and displays its copyable link. Creating a new invitation retries delivery and replaces previous unused links for that address. Account email can be retried through its existing resend/recovery flow.

Logs record failure types and internal IDs without passwords, tokens, message bodies or recipient addresses. SMTP sends use TLS and a 30-second cancellation deadline. No automatic retry queue/outbox is implemented. Never use `docker compose config` without `--quiet` when sharing diagnostic output: expanded configuration can include secrets.

External inbox delivery must be checked after real credentials are supplied. Tests use a captured mailbox and do not send real email.
