# 參與 Polhem.OAuth2 開發

[English](CONTRIBUTING.md) | **繁體中文**

本文說明變更如何進入這個 repo。

## 工作流程

1. fork 這個 repo，並在你的 fork 從最新的 `main` 開分支。
2. 完成變更，並附上測試。修改公開文件或 ADR 時，兩種語言版本要一起更新
   （[ADR-002](docs/adr/adr-002-language-policy.zh-TW.md)）。
3. 在本機建置與測試，指令見 [`.claude/CLAUDE.md`](.claude/CLAUDE.md) 的「Build and test」一節。
4. 對 `main` 開 pull request，Build CI workflow 會在上面執行。

較大的變更（新功能、公開 API 的變更、新增相依套件）請先開 issue，在投入時間之前先取得做法上的共識。

## 誰來合併

這個 repo 只有一位維護者 [@jeff377](https://github.com/jeff377)。其他貢獻者不會取得寫入權限：他們從 fork 作業，維護者審查每個來自
fork 的 pull request 後才合併。維護者自己的變更直接 commit 到 `main`。
