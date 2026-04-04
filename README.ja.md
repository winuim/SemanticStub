# SemanticStub

Semantic-aware な API モックサーバーです。

English: [README.md](README.md)

## YAML 拡張

SemanticStub はベースとなるドキュメント構造に OpenAPI 3.1 を使用し、
カスタム動作には `x-*` フィールドを使用します。`paths`、operation、
`requestBody`、`responses` などの標準 OpenAPI フィールドはそのまま維持し、
SemanticStub 固有の動作だけを拡張フィールドに記述してください。

### サポートしているレスポンス拡張

これらの拡張は response object に対して使用します。

| Extension | Location | Purpose |
| --- | --- | --- |
| `x-delay` | `responses.<status>` または `x-match[].response` | 指定したミリ秒だけレスポンスを遅延させます。 |
| `x-response-file` | `responses.<status>` または `x-match[].response` | YAML ファイルからの相対パスでレスポンスボディを読み込みます。 |
| `x-scenario` | `responses.<status>` または `x-match[].response` | 名前付きシナリオ状態に一致する場合だけレスポンスを有効にし、`next` で次状態に進められます。 |

例:

```yaml
openapi: 3.1.0
info:
  title: Response Extensions Example
  version: 1.0.0

paths:
  /users:
    get:
      responses:
        '200':
          description: User list
          x-delay: 100
          x-response-file: users.json
          content:
            application/json:
              schema:
                type: object
```

補足:

- `x-delay` は 0 以上の整数である必要があります。
- レスポンスは `content` または `x-response-file` のいずれかを定義する必要があります。
- `x-response-file` を使う場合も、media type の宣言自体は `content` に残します。実際の payload は参照ファイルから読み込まれます。
- `x-response-file` のパスは、それを宣言した YAML ファイルからの相対パスとして解決されます。
- ファイルベースのレスポンスでも、`application/octet-stream` など宣言された media type は維持されます。
- 複数の media type が宣言されている場合、SemanticStub は決定的なレスポンス選択のために JSON media type を優先し、それ以外は先頭の宣言を使用します。
- `x-scenario.name` と `x-scenario.state` は必須です。`x-scenario.next` は任意で、そのレスポンスが選ばれた後にインメモリ上のシナリオ状態を進めます。

シナリオ例:

```yaml
paths:
  /checkout:
    post:
      responses:
        '409':
          description: pending
          x-scenario:
            name: checkout-flow
            state: initial
            next: confirmed
          content:
            application/json:
              example:
                result: pending
        '200':
          description: complete
          x-scenario:
            name: checkout-flow
            state: confirmed
          content:
            application/json:
              example:
                result: complete
```

シナリオに関する補足:

- シナリオ状態はインメモリで保持され、シナリオ名単位で共有されます。
- `x-scenario` を持たないレスポンスは常に候補になります。
- レスポンスに `next` が定義されている場合、その後のリクエストでは別の遷移が起きるか、自動 reload か、アプリ再起動でリセットされるまで、進んだ状態が使われ続けます。
- `x-scenario` は通常の `responses` と `x-match[].response` の両方で使用できます。
- シナリオ評価と状態遷移はプロセス内で直列化されるため、同一シナリオフローは並行リクエスト下でも決定的に進みます。

### サポートしている operation 拡張

`x-match` は operation に対して使用でき、標準 OpenAPI の `responses`
が使われる前に条件付きマッチを定義できます。

```yaml
paths:
  /users:
    get:
      x-match:
        - query:
            role: admin
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  users:
                    - id: 1
                      name: Alice
                      role: admin
      responses:
        '200':
          description: Default user list
          content:
            application/json:
              example:
                users: []
```

各 `x-match` エントリには次を含められます。

- `query`: 完全一致の query string 条件
- `x-query-regex`: regex による query string 条件
- `x-query-partial`: 部分一致の query string 条件
- `headers`: 完全一致の header 条件
- `body`: リクエストボディ条件
- `x-semantic-match`: セマンティックフォールバックマッチングに使う自然言語の説明
- `response`: 条件に一致したときに返すレスポンス

補足:

- `response.statusCode` は必須で、正の整数である必要があります。
- `x-match` のレスポンスは、通常レスポンスと同じく `content`、`headers`、`x-delay`、`x-response-file` をサポートします。
- query、header、body 条件は、1 つの `x-match` 内では AND 条件として組み合わされます。
- `x-match` で使う query と header のキーは、path または operation にパラメータ宣言がある場合、その OpenAPI 宣言を参照している必要があります。
- `query` は単一値の完全一致、順序付きの繰り返し値、および `integer`、`number`、`boolean` など宣言済み OpenAPI query parameter type に対する型付き比較をサポートします。
- `x-query-regex` は query 値に対して regex マッチを行います。
- `x-query-partial` は部分文字列一致を行います。複数候補が成功した場合は、完全一致の `query` が regex や partial より優先されます。
- `body` マッチは現在 JSON リクエストボディに対して適用されます。object に対する body マッチは部分一致なので、追加プロパティを含んでいても一致できます。
- 不正な JSON リクエストボディは `body` 条件に一致しません。
- `x-semantic-match` エントリは、すべての決定的な条件が失敗したときのみ評価されます。アプリケーション設定でセマンティックマッチングを有効化する必要があります。`x-semantic-match` を含むエントリに `query`、`x-query-regex`、`x-query-partial`、`headers`、`body` を同時に指定することはできません。
- どの `x-match` も成功しない場合、SemanticStub は標準の `responses` セクションへフォールバックします。
- 複数の `x-match` が成功した場合、SemanticStub はより具体的な候補を選び、狭い条件が広い条件より優先されます。

### セマンティックマッチング

決定的な `x-match` 候補がすべて失敗した場合、SemanticStub はセマンティックマッチングにフォールバックできます。`x-semantic-match` のみを含む `x-match` エントリは、[Text Embeddings Inference](https://huggingface.co/docs/text-embeddings-inference/en/index) エンドポイントのベクトル埋め込みを使い、受信リクエストとのコサイン類似度でスコアリングされます。設定されたしきい値を超えた最高スコアの候補が選択されます。

例:

```yaml
paths:
  /search:
    post:
      x-match:
        - x-semantic-match: find administrator user accounts in the identity directory by email address
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  result: admin-user
        - x-semantic-match: show unpaid billing invoices due this month
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  result: due-invoices
      responses:
        "404":
          description: No match found
```

`appsettings.json` でセマンティックマッチングを設定します:

```json
"SemanticMatching": {
  "Enabled": true,
  "Endpoint": "http://localhost:8081",
  "Threshold": 0.8,
  "TopScoreMargin": 0,
  "TimeoutSeconds": 30
}
```

| 設定 | 説明 | デフォルト |
| --- | --- | --- |
| `Enabled` | セマンティックマッチングフォールバックを有効化します。 | `false` |
| `Endpoint` | TEI エンドポイントのベース URL。 | `""` |
| `Threshold` | マッチを受け入れる最小コサイン類似度 (-1.0〜1.0)。 | `0.8` |
| `TopScoreMargin` | 上位2候補間の最小スコア差。`0` で曖昧性チェックを無効化します。 | `0` |
| `TimeoutSeconds` | 埋め込みエンドポイントへの HTTP リクエストタイムアウト（秒）。 | `30` |

セマンティックマッチングに関する補足:

- リクエスト全体（メソッド、パス、クエリパラメータ、ヘッダー、ボディ）が埋め込みのクエリテキストとして使われます。
- 埋め込みサービスが利用できない場合やタイムアウトした場合、セマンティックマッチングはスキップされ、リクエストは標準の `responses` セクションへフォールバックします。

### マッチング優先順位

リクエスト処理は次の優先順位に従います。

1. template path より exact path を優先
2. 選ばれた path 上での HTTP method 一致
3. operation 上の `x-match` 候補一致（より具体的な完全一致条件が広い条件より優先）
4. 決定的な `x-match` 候補がすべて失敗し、セマンティックマッチングが有効な場合はセマンティックマッチングフォールバック
5. どの `x-match` も成功しない場合は標準 OpenAPI の `responses` にフォールバック

これにより、現在の機能セットでは決定的なルーティングを維持します。

### 現在の制限

- `body` マッチは任意のバイナリリクエストではなく、構造化された JSON リクエストペイロード向けです。

## 開発

- Source: `src/`
- Tests: `tests/`
- Samples: `samples/`

## 実行

```sh
dotnet run --project src/SemanticStub.Api
```

## テスト

```sh
dotnet test
```

## 補足

リポジトリ固有のガイダンスは `AGENTS.md` を参照してください。

## ライセンス

このプロジェクトは MIT ライセンスの下で公開されています。詳細は [LICENSE](LICENSE) ファイルをご覧ください。
