# Personal data key (`PII_ENCRYPTION_KEY`): what breaks on change, and how to rotate

Status: there is no rotation code yet. This page describes what the current code does and what a rotation needs.

## What is encrypted

- Only the user's email is encrypted at rest (`UserConfiguration`). Display name and avatar are stored as they are, by a product decision.
- Algorithm: AES-256-GCM, a random 12-byte nonce per value. Stored form: `v1:` + base64(nonce | ciphertext | tag).
- The column is never queried or indexed, so no lookup depends on the ciphertext.
- The key is `PII_ENCRYPTION_KEY`: base64 of exactly 32 random bytes. Generate it once, keep it outside git (see `deploy/env/main.env.example`).

## What breaks when the key changes

- Every stored email was written under the old key. The new key cannot decrypt it.
- Loading a `User` decrypts its email, so the affected users cannot be read: sign-in and `GET /auth/me` fail for them.
- Changing the key therefore locks out every existing user. It is not a soft failure: the emails are lost unless the old key is kept.
- Losing the key has the same effect. Keep a backup in the team's password manager before the first production start.

## Rotation today

Not possible without data loss. The stored format has no key identifier, so the code cannot tell which key wrote a row.

## What a rotation needs (proposal, not implemented)

1. A key ring: `PII_ENCRYPTION_KEYS` lists `id:base64key` pairs, one of them marked active.
2. A versioned stored prefix: `v<id>:` instead of the fixed `v1:`. Decryption picks the key by the id in the prefix; old rows keep working while the old key stays in the ring.
3. A re-encryption step: a command that reads each `users` row, decrypts with its key and writes it back under the active key. It runs once, then the retired key is removed from the ring.
4. Before the step: a database backup and a check that the re-encrypted count equals the row count.

Until steps 1–3 exist, do not change `PII_ENCRYPTION_KEY` on a running system.
