# semantic-stub-mcp

SemanticStub の Runtime Inspection API を MCP 経由で利用するための TypeScript 製サーバーです。
Claude Desktop など tool 中心のクライアントで使いやすいように、runtime inspection / match simulation / stub generation / match improvement suggestions / scenario reset を tool として公開します。

英語版は [README.md](./README.md) を参照してください。

SemanticStub や TEI を含む全体構成はリポジトリルートの [README.ja.md](../README.ja.md) を参照してください。ここでは MCP サーバー単体の起動手順を説明します。

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
| `get_config` | `GET /_semanticstub/runtime/config` | 設定スナップショット |
| `list_routes` | `GET /_semanticstub/runtime/routes` | ルート一覧 |
| `get_route` | `GET /_semanticstub/runtime/routes/{id}` | ルート詳細 |
| `get_scenarios` | `GET /_semanticstub/runtime/scenarios` | シナリオ状態 |
| `get_metrics` | `GET /_semanticstub/runtime/metrics` | メトリクス |
| `reset_metrics` | `POST /_semanticstub/runtime/metrics/resets` | メトリクスとリクエスト履歴のリセット |
| `get_requests` | `GET /_semanticstub/runtime/requests?limit=` | 件数指定つきリクエスト履歴 |
| `test_match` | `POST /_semanticstub/runtime/test-match` | マッチ確認（副作用なし） |
| `explain_match` | `POST /_semanticstub/runtime/explain` | マッチ詳細説明 |
| `get_last_explain` | `GET /_semanticstub/runtime/explain/last` | 直近の explain 結果 |
| `reset_scenario_state` | `POST /_semanticstub/runtime/scenarios/resets` / `POST /_semanticstub/runtime/scenarios/{name}/resets` | シナリオ状態のリセット |
| `export_stubs_as_yaml` | `GET /_semanticstub/runtime/requests/export/yaml` / `GET /_semanticstub/runtime/requests/{index}/export/yaml` | 記録済みリクエストを YAML スタブドラフトとして出力 |
| `suggest_improvements` | `GET /_semanticstub/runtime/requests/{index}/suggest-improvements` / `POST /_semanticstub/runtime/suggest-improvements` | あいまいな定義に対する YAML 改善候補を提示 |

## 入力メモ

- `test_match` / `explain_match` / `suggest_improvements` の `body` は JSON object ではなく raw string です。
- JSON body を送りたい場合は、たとえば `"{\"message\":\"hello\"}"` のように文字列化して渡してください。
- `includeCandidates` のデフォルトは `test_match` では `false`、`explain_match` では `true` です。
- `includeSemanticCandidates` を指定すると、semantic matching 実行時の候補スコアを含められます。
- `test_match` と `explain_match` の結果には、該当する場合に response id、status code、source（`responses` または `x-match`）、candidate index などの selected response 情報も含まれます。
- `export_stubs_as_yaml` は YAML テキストをそのまま返します。出力はドラフトなので、`TODO` プレースホルダーを埋めてから利用してください。
- `suggest_improvements` は `index`（記録済みリクエストを分析）または `method` + `path`（仮想リクエストを分析）のいずれかで呼び出します。`method`/`path` を使う場合は両方必須です。

## 制約

- `metrics` / `requests` / `get_last_explain` / `scenarios` は process-local な runtime 状態を返します。
- `reset_metrics` は aggregate metrics と recent request history だけを消去します。scenario state、active stub definitions、直近の real-request explanation は変更しません。
- この MCP サーバー自体は SemanticStub HTTP API への薄い委譲層であり、YAML や core behavior は変更しません。

## 開発時

```bash
# ビルドなしで直接実行
npm run dev
```

この MCP パッケージが依存する inspection API の契約は、
[`tests/SemanticStub.Api.Tests/Integration/StubInspectionEndpointTests.cs`](../tests/SemanticStub.Api.Tests/Integration/StubInspectionEndpointTests.cs)
で回帰テストしています。

## 環境変数

| 変数名 | デフォルト | 説明 |
|---|---|---|
| `SEMANTIC_STUB_URL` | `http://localhost:8080` | SemanticStub API のベースURL |
