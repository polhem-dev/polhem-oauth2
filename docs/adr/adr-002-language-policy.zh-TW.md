# ADR-002：共同維護的內容一律英文，公開文件中英雙語

[English](adr-002-language-policy.md) | **繁體中文**

## 狀態

已採納（2026-09-13）

## 背景

Polhem 的專案預計由多人共同維護。Bee.OAuth2 是單人維護的，XML 文件、註解與 commit message 都用中文，
範圍更廣的貢獻者看不懂。另一方面，它的使用者裡有仰賴繁體中文文件的讀者。

## 決策

- 共同維護的內容一律用英文：原始碼、XML 文件（也會隨套件出現在 IntelliSense）、註解、測試方法名稱與
  `[DisplayName]` 文字、commit message，以及 `.claude/` 底下給 agent 的指引。
- 公開的 Markdown 文件中英雙語。`name.md` 是英文版、`name.zh-TW.md` 是繁體中文版，兩份頂部互相連結。
- ADR 比照公開文件辦理，讓每一位貢獻者都讀得懂程式碼為什麼是現在這個樣子。

## 影響

- 修改公開文件或 ADR 時，要在同一個 commit 裡同步更新兩種語言。目前沒有自動檢查把關這件事。
- `.claude/CLAUDE.md` 會明文寫出這項政策，因為個人的 AI 助理設定可能預設使用其他語言。
