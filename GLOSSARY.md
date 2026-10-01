# Auth Platform

Central identity, authentication, and authorization platform supporting OIDC provider, member management, and developer applications.

## Language

**Member**:
A registered individual identity in the platform holding credentials, profile information, and authorized grants.
_Avoid_: User, Account, Customer

**MemberSnapshot**:
An immutable domain snapshot of a member retrieved by the authorization server for token issuance, consent verification, and session validation.
_Avoid_: UserDto, MemberInfo, MemberRecord

**SecurityStamp**:
A cryptographic token attached to a member's credentials and claims principal, regenerated upon password modification to invalidate all historical sessions and refresh tokens.
_Avoid_: TokenVersion, SessionVersion, Stamp

**ConsentTicket**:
A time-limited (5-minute), data-protected encrypted authorization context passed to the member consent UI to capture explicit user authorization decisions.
_Avoid_: ConsentToken, AuthTicket, GrantTicket

**LoginLockoutPolicy**:
A domain policy governing consecutive failed login counters, temporary account lockouts, and automatic lockout expiration.
_Avoid_: RateLimiter, BlockRule, LockoutService

**DualScheme**:
An authentication mechanism enabling API endpoints to dynamically accept either first-party browser session cookies or OAuth 2.0 Bearer tokens based on caller context.
_Avoid_: HybridAuth, MultiAuth, DoubleScheme
