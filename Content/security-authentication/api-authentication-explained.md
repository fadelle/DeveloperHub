---
title: "API Authentication Explained: Keys, Tokens, OAuth, and JWTs"
slug: api-authentication-explained
summary: API Key, OAuth, Bearer Token, JWT, Client Secret — a dozen related terms that get used interchangeably despite answering different questions, and a decision table for which one actually fits a given integration.
order: 1
sources: [API_Authentication_Guide_FIXED.docx]
related: []
---

## Problem

API Key, OAuth 2.0, Bearer Token, Access Token, Refresh Token, Client ID, Client Secret, JWT, Basic Auth, Session Cookie, OIDC, PAT, HMAC Signature, mTLS, PKCE — these terms get used as if they're roughly interchangeable, but they answer genuinely different questions, and picking the wrong one for a given integration is a real, recurring mistake (embedding a client secret in a mobile app, using a bearer token where a signed webhook request was needed, confusing an access token's job with a refresh token's). Three questions sit underneath almost all of this vocabulary, and keeping them separate resolves most of the confusion:

- **Who are you?** → Authentication
- **What can you do?** → Authorization
- **How do you prove it?** → Keys, tokens, credentials

## When It Applies

Reach for this case whenever integrating with or building an API that needs to authenticate its callers — choosing between an API key, an OAuth flow, a JWT, or a session cookie; reviewing whether a client secret is being used somewhere it shouldn't be; or just translating between terms that keep showing up in the same conversations without a clear shared meaning.

## Solution

The main terms, what each one actually is, and where it typically shows up:

| Term | What it is | Common use |
|---|---|---|
| API Key | A secret value that identifies or authenticates an application | Simple API integrations |
| OAuth 2.0 | A framework for obtaining limited access to resources | Google, Microsoft, GitHub integrations |
| Bearer Token | A token that grants access to whoever possesses it | Calling protected APIs |
| Access Token | A credential used to access protected resources | OAuth API calls |
| Refresh Token | A credential used to request new access tokens | Maintaining access without repeated login |
| Client ID | A public identifier for an application | OAuth registration and flows |
| Client Secret | A confidential, password-like credential for an application | Backend OAuth clients |
| JWT | A structured token format containing claims, often digitally signed | Identity and access tokens |
| Basic Auth | A scheme that sends a username and password (or equivalent) | Simple and legacy APIs |
| Session Cookie | A browser credential associated with a session | Website logins |
| OpenID Connect (OIDC) | An identity layer on top of OAuth 2.0 | Sign in with Google |
| Personal Access Token (PAT) | A user-created token for API access | GitHub, GitLab, developer tools |
| HMAC Signature | Cryptographic proof generated with a shared secret | Signed API requests and webhooks |
| mTLS | Mutual certificate-based authentication | Bank and enterprise APIs |
| PKCE | A security mechanism protecting OAuth authorization-code flows | Mobile apps, SPAs, modern OAuth |

**How the core pieces actually work:**

**API Key** — the provider gives an application a key; the application sends it with every request. Usually simple and long-lived, and it typically identifies an *application*, not an individual logged-in user.

```
GET /api/data
x-api-key: abc123xyz
```

**OAuth 2.0** — a process for obtaining access tokens with defined permissions, often after a user explicitly approves access. Example: an app requests permission to read a user's Google Calendar without ever learning their Google password.

**Bearer Token** — a *method of using* a token, not a kind of token: whoever possesses a valid bearer token can generally use it. OAuth access tokens are often bearer tokens, but "OAuth" and "Bearer" are not synonyms — one is the process for obtaining access, the other is how the resulting credential gets presented.

```
GET /api/data
Authorization: Bearer eyJhbGci...
```

**Client ID and Client Secret** — the Client ID identifies the application and can be public. The Client Secret authenticates a *confidential* application and must remain private — it should never be embedded in browser or mobile application code, since anything shipped to a client device can be extracted.

```
client_id     = my_app_123
client_secret = secret_xyz
```

**JWT (JSON Web Token)** — a token *format*, not a kind of authentication on its own. It can contain claims like user ID, issuer, and expiration. A JWT can be an OAuth access token, an OIDC ID token, or something else entirely — not every JWT is an access token, and not every access token is a JWT.

```
header.payload.signature
```

**Key idea to hold onto:** OAuth is the *process* for getting access; the access token is what the application actually uses afterward to call the API.

## Example: OAuth 2.0 Authorization Code Flow — Connecting to Google

A simplified walkthrough of connecting an application to Google using an OAuth authorization-code flow: the user is redirected to Google, approves the requested access, and Google redirects back with a short-lived authorization code. The application exchanges that code (plus, for confidential clients, its client secret) for an access token at Google's token endpoint. From that point forward, the application uses the access token to call the Google API on the user's behalf — it never sees the user's Google password at any point in this flow.

**Expected behavior:** the access token the app receives is scoped to whatever permissions the user actually approved (for example, read-only calendar access) — not full account access — and it typically expires, at which point a refresh token (if one was issued) can be used to obtain a new access token without asking the user to log in again.

## Example: Choosing the Right Mechanism for Your Situation

| Your situation | Typical choice |
|---|---|
| Calling a simple service API | API Key |
| Connecting a user's Google or Microsoft account | OAuth 2.0 |
| Calling an API after OAuth authorization | Bearer Access Token |
| Logging users into your application | OIDC, usually with sessions |
| Backend service communicating with another backend | OAuth Client Credentials, mTLS, or signed credentials |
| Running a GitHub automation script | Personal Access Token or GitHub App token |
| Authenticating browser sessions | Secure Session Cookies |
| Verifying signed webhook requests | HMAC Signature |
| Building a public mobile or single-page app with OAuth | Authorization Code + PKCE |

**Expected use of this table:** match the *situation* first (who's calling, what trust relationship already exists, whether a human is present to approve access) before reaching for a specific mechanism by habit or familiarity — a mechanism chosen because it's the one used elsewhere in the codebase, rather than because it fits this specific situation, is a common source of the mismatches covered next.

## Tradeoffs & Common Mistakes

Several distinctions get confused often enough to call out explicitly:

- **OAuth vs JWT.** OAuth is a protocol framework; JWT is a token *format*. They're not alternatives to each other — a system can use OAuth with JWTs as the token format, or OAuth with opaque (non-JWT) tokens.
- **JWT vs Bearer.** JWT describes the token's internal structure; Bearer describes *how* a token is presented and used on the wire. A token can be both a JWT and a bearer token at once — these describe different aspects of the same credential.
- **API Key vs Client Secret.** An API key may authenticate API requests directly, on its own. A client secret generally authenticates an OAuth *client* to the authorization server — it's a different credential for a different trust relationship, even though both are "a secret string an application holds."
- **Access Token vs Refresh Token.** The access token accesses resources; the refresh token obtains *replacement* access tokens. Using a refresh token as if it were an access token (or vice versa) doesn't work and isn't just a naming inconsistency — they have different scopes, lifetimes, and intended use.
- **OAuth vs OIDC.** OAuth handles delegated *access* (can this app read my calendar?); OIDC adds standardized user *authentication* on top (who is this user, actually?) — OAuth alone doesn't guarantee the caller knows who the user is, which is exactly the gap OIDC closes.
- **Authentication vs Authorization.** Authentication determines identity (who are you); authorization determines permissions (what can you do). A system can authenticate someone correctly and still get authorization wrong, and the two failure modes need different fixes.
- **The most dangerous mistake in this list:** embedding a Client Secret (or any confidential credential) in code that ships to a browser or mobile device. Anything in client-side code is extractable by definition — confidential credentials belong on a server the client doesn't have access to.

## Alternatives

Four more OAuth-adjacent terms worth knowing, since they show up constantly around the core flow:

- **Scopes** — the specific permissions being requested, such as `calendar.readonly`. Requesting narrower scopes than an application actually needs is a common and easy security improvement.
- **Authorization Code** — a short-lived, one-time code exchanged for tokens — this is the thing that gets passed around during the redirect in the authorization-code flow, not the access token itself.
- **Token Endpoint** — the server endpoint where clients actually obtain or refresh tokens, as opposed to the authorization endpoint where the user approves access.
- **Token Expiration** — limits how long a token can be used; a refresh token can sometimes extend access without requiring the user to consent again, within whatever policy the authorization server enforces.

**Security note that applies to every mechanism above:** protect all secrets and tokens as you would any credential, and treat HTTPS as non-negotiable — every HTTP example in this case assumes a secure connection; none of these mechanisms protect a credential sent over plain HTTP.
