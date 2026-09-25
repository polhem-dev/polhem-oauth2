#!/usr/bin/env bash
# Checks the generator that writes the settings OAuthMaui packages (samples/OAuthMaui/build/GenerateOAuthApp.cs):
# settings with a client secret in every client section must produce output without one, and a copy of the generator
# whose allowlist lets ClientSecret through must fail instead of writing it. Run it from the repository root.
set -euo pipefail

generator=samples/OAuthMaui/build/GenerateOAuthApp.cs
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

cat > "$work/config.json" <<'JSON'
{
  "Providers": {
    "Okta": {
      "Domain": "dev-1.okta.com",
      "Desktop": { "ClientId": "desktop", "ClientSecret": "secret-1", "RedirectUri": "http://127.0.0.1:0/callback" },
      "Web": { "ClientId": "web", "ClientSecret": "secret-2", "RedirectUri": "https://localhost:7032/auth/callback" },
      "iOS": { "ClientId": "ios", "ClientSecret": "secret-3", "RedirectUri": "dev.example:/oauth2redirect" },
      "Android": { "ClientId": "android", "ClientSecret": "secret-4", "RedirectUri": "dev.example:/oauth2redirect" }
    }
  },
  "AppRelay": { "BackendUrl": "https://localhost:7032", "RedirectUri": "dev.example:/relay", "ClientSecret": "secret-5" }
}
JSON

dotnet run "$generator" -- "$work/config.json" "$work/out" dev.example
if grep -qi secret "$work/out/OAuthApp.json"; then
  echo "error: the generator packaged a secret:"
  cat "$work/out/OAuthApp.json"
  exit 1
fi

sed 's/"UsePkce" };/"UsePkce", "ClientSecret" };/' "$generator" > "$work/Loosened.cs"
if cmp -s "$generator" "$work/Loosened.cs"; then
  echo "error: the allowlist line of the generator changed, so this script cannot loosen it. Update the sed expression."
  exit 1
fi
if dotnet run "$work/Loosened.cs" -- "$work/config.json" "$work/out-loosened" dev.example; then
  echo "error: the generator wrote settings with a secret instead of failing."
  exit 1
fi

echo "The OAuthMaui settings generator keeps secrets out, and fails when its allowlist would let one through."
