# semantic-stub-mcp

SemanticStub の Runtime Inspection API を MCP 経由で利用するための TypeScript 製サーバーです。
Claude Desktop など tool 中心のクライアントで使いやすいように、runtime inspection / match simulation / scenario reset を tool として公開します。

英語版は [README.md](./README.md) を参照してください。

通常の利用は、リポジトリルートの [README.ja.md](../README.ja.md) にある
Docker Compose 構成を推奨します。ここでは `mcp` を単体で動かす場合の手順を説明します。

## 必要なもの

- Node.js 18+
- SemanticStub API が起動していること

## セットアップ

```bash
cd mcp
npm install
npm run build
```

## Claude Desktop への登録

`claude_desktop_config.json` に追加します。

**macOS**
```
~/Library/Application Support/Claude/claude_desktop_config.json
```

```json
{
  "mcpServers": {
    "semantic-stub": {
      "command": "node",
      "args": ["/path/to/SemanticStub/mcp/dist/index.js"],
      "env": {
        "SEMANTIC_STUB_URL": "http://localhost:8080"
      }
    }
  }
}
```

## 提供 tools

| ツール名 | 対応エンドポイント | 説明 |
|---|---|---|
| `get_config` | `GET /runtime/config` | 設定スナップショット |
| `list_routes` | `GET /runtime/routes` | ルート一覧 |
| `get_route` | `GET /runtime/routes/{id}` | ルート詳細 |
| `get_scenarios` | `GET /runtime/scenarios` | シナリオ状態 |
| `get_metrics` | `GET /runtime/metrics` | メトリクス |
| `reset_metrics` | `POST /runtime/metrics/reset` | メトリクスとリクエスト履歴のリセット |
| `get_requests` | `GET /runtime/requests?limit=` | 件数指定つきリクエスト履歴 |
| `test_match` | `POST /runtime/test-match` | マッチ確認（副作用なし） |
| `explain_match` | `POST /runtime/explain` | マッチ詳細説明 |
| `get_last_explain` | `GET /runtime/explain/last` | 直近の explain 結果 |
| `reset_scenario_state` | `POST /runtime/scenarios/reset` / `POST /runtime/scenarios/{name}/reset` | シナリオ状態のリセット |

## 入力メモ

- `test_match` と `explain_match` の `body` は JSON object ではなく raw string です。
- JSON body を送りたい場合は、たとえば `"{\"message\":\"hello\"}"` のように文字列化して渡してください。
- `includeSemanticCandidates` を指定すると、semantic matching 実行時の候補スコアを含められます。

## 制約

- `metrics` / `requests` / `get_last_explain` / `scenarios` は process-local な runtime 状態を返します。
- `reset_metrics` は aggregate metrics と recent request history だけを消去します。scenario state、active stub definitions、直近の real-request explanation は変更しません。
- この MCP サーバー自体は SemanticStub HTTP API への薄い委譲層であり、YAML や core behavior は変更しません。

## 開発時

```bash
# ビルドなしで直接実行
npm run dev
```

## 環境変数

| 変数名 | デフォルト | 説明 |
|---|---|---|
| `SEMANTIC_STUB_URL` | `http://localhost:8080` | SemanticStub API のベースURL |
