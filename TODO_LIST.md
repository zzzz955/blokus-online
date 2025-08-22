# AI Development Rules & Guidelines

## Work Execution Rules
1. **Sequential Task Processing**: Pick ONE task from Todo List and complete it fully before moving to next
2. **Code Reuse Priority**: Always check for existing similar functionality and extend/modify rather than creating new files
3. **Decision Points**: Stop and ask user when facing unclear requirements or ambiguous implementation choices
4. **Implementation Authority**: Full autonomy for file creation/modification, only consult on unclear specifications
5. **Completion Protocol**: Report completion summary and check off TodoList items when done

## Environment Configuration
- **Development**: `http://localhost:9000` (OIDC), `http://localhost:3000` (Web), `http://localhost:8080` (Desktop)
- **Production**: `https://blokus-online.mooo.com` (All services behind reverse proxy)
- **Mobile Deep Link**: `blokus://auth/callback` (Custom URL schema for Unity)

## Token Lifecycle Policy (Updated)
- **Access Token**: 10 minutes (RS256 JWT)
- **Refresh Token**: 30 days sliding window (max 90 days absolute)
- **Authorization Code**: 10 minutes
- **Sliding Window**: Each refresh extends +30 days, capped at 90 days from initial login

## Completed Implementations
✅ **OIDC Authorization Server** (Port 9000)
- Complete OAuth 2.1/OIDC endpoints: discovery, authorize, token, jwks, introspect, revocation
- RS256 JWT signing with key rotation support
- PKCE support for public clients (Qt, Unity)
- Client secret authentication for confidential clients (Web)
- Sliding window refresh token policy (30d+90d max)
- Reuse detection with family revocation
- Admin API for token management and monitoring

✅ **Token Security Features**
- Refresh token rotation on every use
- Family-based token management with reuse detection
- Sliding window expiration (30 days renewable, 90 days max)
- RS256 signature with configurable key rotation

✅ **Database Schema Implementation**
- OIDC authorization_codes table with PKCE support
- Refresh token families with device fingerprinting
- Individual refresh tokens with rotation chain tracking
- Sliding window support with max_expires_at field
- Comprehensive indexing for performance
- Automatic cleanup functions and triggers

✅ **API Server OIDC Integration** (Port 8080)
- JWKS-based JWT verification with RS256 support
- `/api/auth/login` → OIDC redirect with client type detection
- `/api/auth/oidc-discovery` → OIDC server endpoints and client configs
- `/api/auth/refresh` → OIDC token refresh guidance
- `/api/auth/validate` → OIDC JWT token validation (JWKS cached)
- `/api/auth/info` → OIDC user info extraction with standard claims
- Guest token generation maintained for offline play
- Environment-specific configurations (dev/prod)
- Legacy compatibility with user_id mapping from sub claim

✅ **TCP Server JWT Authentication** (Port 9999)
- JWT verification with RS256 and JWKS support using jwt-cpp library
- JWKS client with background refresh (10-minute cache, 5-minute refresh)
- Hybrid authentication: `AUTH <username>:<password>` OR `AUTH <JWT_token>`
- JWT token auto-detection based on '.' separators (3-part structure)
- RS256 signature verification with audience/issuer validation
- 30-second grace period for token expiration (exp + 30s tolerance)
- Claims extraction: sub, preferred_username, email, iat, exp, nbf
- Integration with existing session management and user account system
- Backward compatibility with existing username/password authentication

✅ **Qt Client OIDC Authentication** (Desktop)
- Complete OIDC Authorization Code + PKCE implementation
- PKCE code challenge/verifier generation with SHA256 base64url encoding
- System browser launch via QDesktopServices for OAuth authorization
- HTTP loopback server (QTcpServer) for authorization code capture
- Dynamic port allocation (8080-8090 range) with timeout handling
- Secure token storage using Windows Credential Manager (+ cross-platform fallback)
- JWT token integration with existing NetworkClient authentication flow
- Refresh token rotation logic with automatic token renewal
- Complete LoginWindow UI integration with "Google로 로그인" button
- Error handling, loading states, and user feedback

---

# Context
- Monorepo components:
  1) tcp-server (C++/Boost.Asio): custom password + server-side session token; login success message "AUTH_SUCCESS:<username>:<sessionToken>:...".
  2) api-server (Node/Express): issues short-lived JWT on /api/auth/login; /api/auth/refresh is NOT supported (advises to re-auth via the main auth server).
  3) web-server (Next.js/TypeScript): uses NextAuth with Google provider and argon2 hashing helpers; currently cookie/JWT session is separate from API/tcp flows.
  4) client-desktop (Qt/C++): connects directly to tcp-server.
  5) client-mobile (Unity): calls API server for single-player; future: use system-browser OAuth.
  6) database: users table exists; NO refresh-token family tables yet.

- Current pain:
  * No centralized IdP/OIDC server.
  * API JWT has no refresh/rotation; /refresh intentionally disabled.
  * TCP relies on custom session tokens (not JWT).
  * Web uses NextAuth independently (cookies/JWT), not unified with mobile/desktop.
  * No refresh-token rotation or reuse detection. No token family storage.

- Goal:
  Design and implement a unified OAuth 2.1 / OIDC-based auth across web, mobile, desktop, API, and TCP server, with refresh-token rotation and reuse detection.

# Requirements
1. Introduce a dedicated OIDC Authorization Server (can be custom or OSS).
2. Web uses BFF with httpOnly cookies; NextAuth becomes an OIDC *client* to the new IdP.
3. Mobile (Unity) and Desktop (Qt) use Authorization Code + PKCE via system browser (loopback/custom URI).
4. API server only verifies JWT (RS256/ES256) via JWKS; no token issuing there.
5. TCP server verifies JWT during handshake and binds user_id from `sub` claim; remove password-based login long term.
6. Implement refresh-token rotation and reuse detection with token families (DB-backed).
7. Centralize logout and token-family revocation (graceful TTL & forced logout propagation).
8. Add test scenarios: login, refresh/rotation, token expiry, reuse attack detection, logout.

# Todo List
- [x] Stand up OIDC Auth Server with endpoints: /.well-known/openid-configuration, /authorize, /token, /jwks.json, /introspect (optional), /revocation.
- [x] Define token lifetimes: Updated to AT=10m, RT=30d sliding (max 90d), with rotation on every refresh.
- [x] DB schemas: refresh_token_family, refresh_token (jti, prev_jti, status, expires_at, last_used_at, device_fingerprint, max_expires_at).
- [x] Implement reuse detection → on seeing an old RT jti used twice, revoke the whole family.
- [x] API server: replace local /login with redirect/links to IdP; keep JWT middleware (JWKS cached).
- [x] TCP server: add RS256 JWT verifier (kid support), first message AUTH <JWT>; handle exp/nbf/aud/iss; add 30s grace period for re-auth.
- [x] Qt client: PKCE flow with system browser + loopback; store tokens in OS secure storage; implement silent refresh via RT rotation.
- [ ] Unity client: system browser + app link; secure local storage (Keychain/Keystore); same refresh mechanics.
- [ ] Web (Next.js): BFF pattern; NextAuth configured as OIDC client; server-to-server calls use AT, not exposing AT to the browser.
- [ ] Logout flows: revoke RT family; clear web session cookie; notify TCP server to drop sessions (optional via pub/sub).
- [ ] Tests: end-to-end scripts for login, refresh, RT reuse attack, TTL expiry, global logout.

# Constraints
- Keep existing languages: C++ (Boost.Asio), Node/TS (Next.js/Express), Unity C#.
- Follow OAuth 2.1/OIDC best practices; use RS256 keys with key rotation (kid, JWKS cache).
- UX: always use system browser for OAuth; minimize re-prompts; add grace periods in TCP.

# Output format
- Provide: 
  1) a step-by-step execution plan per repository (diff-level bullets), 
  2) sequence diagrams for web, desktop, mobile, tcp handshakes, 
  3) minimal code snippets (Node JWT middleware with JWKS; C++ RS256 verify skeleton; Unity/Qt PKCE flow outline), 
  4) DB migrations for token families, 
  5) test checklist (login/refresh/expiry/reuse/logout).
