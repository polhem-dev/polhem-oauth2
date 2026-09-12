# Polhem.OAuth2.Desktop

Polhem.OAuth2.Desktop is a Windows Forms library that provides a user interface for OAuth2 authentication. It supports **Google, Facebook, LINE, Azure, and Auth0** authentication with an easy-to-use UI component.

## 📦 Installation

Install via NuGet Package Manager:

```sh
dotnet add package Polhem.OAuth2.Desktop
```

## 🌍 Supported OAuth2 Providers

- ✅ Google
- ✅ Facebook
- ✅ LINE
- ✅ Azure (Microsoft Entra ID)
- ✅ Auth0

## 🚀 Usage Example

### Google OAuth2 Authentication

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.Desktop;

private async void GoogleOAuth2()
{
    var options = new GoogleOAuth2Options()
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "http://localhost:5000/callback",
        UsePKCE = true
    };
    var client = new OAuth2Client(options)
    {
        Caption = "Google Login",
        Width = 600,
        Height = 800
    };

    // Register the client globally
    OAuth2Manager.RegisterClient("Google", client);

    // Execute login
    var result = await OAuth2Manager.Login("Google");
    var userinfo = $"UserID : {result.UserInfo.UserId}\r\n" +
                   $"UserName : {result.UserInfo.UserName}\r\n" +
                   $"Email : {result.UserInfo.Email}\r\n" +
                   $"RawJson : \r\n{result.UserInfo.RawJson}";
}
```

## 📜 License

This project is licensed under the MIT License.

---

# Polhem.OAuth2.Desktop（中文）

Polhem.OAuth2.Desktop 是一個 Windows Forms 函式庫，提供 OAuth2 驗證的使用者介面。支援 **Google、Facebook、LINE、Azure 以及 Auth0** 的身份驗證，並內建易於使用的 UI 元件。

## 📦 安裝方式

透過 NuGet 安裝：

```sh
dotnet add package Polhem.OAuth2.Desktop
```

## 🌍 支援的 OAuth2 提供者

- ✅ Google
- ✅ Facebook
- ✅ LINE
- ✅ Azure（Microsoft Entra ID）
- ✅ Auth0

## 🚀 使用範例

### Google OAuth2 驗證

```csharp
using Polhem.OAuth2;
using Polhem.OAuth2.Desktop;

private async void GoogleOAuth2()
{
    var options = new GoogleOAuth2Options()
    {
        ClientId = "your-client-id",
        ClientSecret = "your-client-secret",
        RedirectUri = "http://localhost:5000/callback",
        UsePKCE = true
    };
    var client = new OAuth2Client(options)
    {
        Caption = "Google Login",
        Width = 600,
        Height = 800
    };

    // 註冊這個 client 到全域管理器
    OAuth2Manager.RegisterClient("Google", client);

    // 執行登入流程
    var result = await OAuth2Manager.Login("Google");
    var userinfo = $"UserID : {result.UserInfo.UserId}\r\n" +
                   $"UserName : {result.UserInfo.UserName}\r\n" +
                   $"Email : {result.UserInfo.Email}\r\n" +
                   $"RawJson : \r\n{result.UserInfo.RawJson}";
}
```

## 📜 授權

本專案採用 MIT License。
