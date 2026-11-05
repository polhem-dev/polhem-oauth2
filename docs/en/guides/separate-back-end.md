# Applications with a back end

**English** | [繁體中文](../../zh-TW/guides/separate-back-end.md)

An application without a back end of its own signs in directly and uses the result itself: `LoopbackOAuth2Client` in a
[desktop or console application](desktop.md), and `AppOAuth2Client` in a [.NET MAUI application](maui.md).

An application with a back end has the back end sign in, with every provider, OpenID Connect providers included. The back
end learns who signed in from the provider itself, the provider's tokens and the client secret stay on the back end, and
the back end issues its own session:

| Front end | How it signs in |
|-----------|-----------------|
| Web pages, including single-page applications | The back end runs the web sign-in: [ASP.NET Core](aspnetcore.md) or [ASP.NET (System.Web)](aspnet-system-web.md) |
| .NET MAUI on Android, iOS and Mac Catalyst | Through the back-end relay, with `WebAuthenticator`: [Signing in through the back end](maui.md#signing-in-through-the-back-end) |
| Desktop and console applications, and .NET MAUI on Windows | Through the back-end relay, with `LoopbackRelayClient`: [Signing in through the back end](desktop.md#signing-in-through-the-back-end) |

Do not send the back end anything the front end obtained itself as proof of who signed in: the back end cannot tell user
information that was forged, and a token is not enough either. Another application that the same user signed in to holds a
valid token for that user, which the provider's user information endpoint answers with the same user.

The relay is part of Polhem.OAuth2.AspNetCore. A back end on another framework can follow the same steps with
`OAuth2Client`, as [Other server frameworks](other-server-frameworks.md) describes for the web sign-in.

Version 1.5.0 removed `OAuth2Client.GetUserInfoAsync(accessToken)` and `OAuth2Options.AcceptedClientIds`, which returned
the user of an access token that a front end sent. Move that sign-in to the back end as the table shows.
[ADR-011](../../adr/adr-011-back-end-signs-in.md) records the reasons.
