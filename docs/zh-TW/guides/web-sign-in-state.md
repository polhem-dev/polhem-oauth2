# 網頁應用程式：登入狀態怎麼保存

[English](../../en/guides/web-sign-in-state.md) | **繁體中文**

本文適用於 [ASP.NET Core](aspnetcore.md) 與 [ASP.NET（System.Web）](aspnet-system-web.md)。

- 每次登入都把 state、PKCE code verifier、回呼網址與 client 名稱，放在一個專屬的 cookie 裡，
  以 ASP.NET Core 的 data protection 或 `MachineKey` 加密並驗證。不需要 session，在多個分頁同時登入也不會互相覆蓋。
  見 [ADR-005](../../adr/adr-005-web-sign-in-cookie.zh-TW.md)。
- cookie 名稱以 `__Host-` 開頭，並設為 `Secure`、HTTP-only、`SameSite=Lax`，所以登入的起點與回呼頁面都必須走 HTTPS。
  provider 從它自己的網站以 form POST 回來的 client（例如 Sign in with Apple，見 `OAuth2Client.UsesFormPostResponse`）改用
  `SameSite=None`，瀏覽器才會在那個請求帶上它。
- 回呼時，GET 請求讀 query，POST 請求讀 form。form POST 的 client 要讓回呼同時接受兩種方法，並排除在 antiforgery 驗證之外
  （例如 `[IgnoreAntiforgeryToken]`），因為 provider 的頁面不會送 token，回呼由 state 保護。cookie policy 的 `MinimumSameSitePolicy`
  比 None 嚴格，或 System.Web 沒有 .NET Framework 4.7.2 的 2019 年 11 月更新時，瀏覽器不會送出這個 cookie，登入會失敗。
- 登入必須在 10 分鐘內完成。回呼時會先刪除 cookie，再用授權碼換 token。
- 所有可能收到回呼的伺服器都必須能解開 cookie：ASP.NET Core 要共用 data protection 的金鑰環，System.Web 要使用相同的 machine key。
