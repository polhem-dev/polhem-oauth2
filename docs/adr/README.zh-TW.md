# 架構決策紀錄

[English](README.md) | **繁體中文**

每一份紀錄說明 Polhem.OAuth2 的一項設計決策，以及背後的理由。

| 紀錄 | 決策 |
|------|------|
| [ADR-001](adr-001-drop-bee-base.zh-TW.md) | 移除 Bee.Base 相依，state 加密改由套件自帶 |
| [ADR-002](adr-002-language-policy.zh-TW.md) | 共同維護的內容一律英文，公開文件中英雙語 |
| [ADR-003](adr-003-exception-semantics.zh-TW.md) | 只有預期的 OAuth2 失敗會轉成失敗結果 |
| [ADR-004](adr-004-system-browser-loopback.zh-TW.md) | 桌面應用程式改由系統瀏覽器加 loopback 回呼登入 |
