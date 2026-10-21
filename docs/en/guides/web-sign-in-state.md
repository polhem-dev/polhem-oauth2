# Web applications: how a sign-in is kept

**English** | [繁體中文](../../zh-TW/guides/web-sign-in-state.md)

This applies to [ASP.NET Core](aspnetcore.md) and [ASP.NET (System.Web)](aspnet-system-web.md).

- Each sign-in keeps its state, PKCE code verifier, redirect URI and client name in a cookie of its own, encrypted and
  authenticated with ASP.NET Core data protection or `MachineKey`. No session state is needed, and sign-ins started in
  several tabs do not replace each other. See [ADR-005](../../adr/adr-005-web-sign-in-cookie.md).
- The cookie name starts with `__Host-`, and the cookie is `Secure`, HTTP-only and `SameSite=Lax`, so the sign-in must
  start and end on HTTPS pages. A client whose provider returns with a form POST from its own site, as Sign in with Apple
  does (`OAuth2Client.UsesFormPostResponse`), gets `SameSite=None` instead, which a browser sends with that request.
- The callback reads the query of a GET request and the form of a POST request. For a form POST client, map the callback for
  both methods and leave it out of antiforgery validation, for example with `[IgnoreAntiforgeryToken]`, because the
  provider's page sends no token; the state protects the callback. A cookie policy whose `MinimumSameSitePolicy` is stricter
  than None, and System.Web without the November 2019 update of .NET Framework 4.7.2, keep the browser from sending that
  cookie, and the sign-in fails.
- A sign-in must complete within 10 minutes. The callback removes the cookie before it exchanges the code.
- Every server that can receive the callback must be able to decrypt the cookie: share the data protection key ring in
  ASP.NET Core, or use the same machine key on System.Web.
