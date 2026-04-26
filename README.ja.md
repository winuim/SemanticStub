# SemanticStub

Semantic-aware な API モックサーバーです。


English: [README.md](README.md)

## 概要

SemanticStub は、ローカル開発、テスト、AI 支援ワークフロー向けの semantic-aware な API モックサーバーです。

決定的な OpenAPI ベースのルーティングと、必要に応じたセマンティックマッチングを組み合わせることで、厳密なモック動作を定義しつつ、自然言語ベースのフォールバックシナリオにも対応できます。

### 主な機能

- OpenAPI 3.1 ベースの YAML stub 定義と、SemanticStub 固有の動作を記述する `x-*` 拡張。
- query string、header、JSON body、form-urlencoded body に対する条件付きリクエストマッチング。
- Text Embeddings Inference (TEI) エンドポイントを利用したオプションのセマンティックマッチング。
- インメモリ state transition によるシナリオベースのレスポンスフロー。
- route、scenario、metrics、recent requests、match explanation を確認できる runtime inspection endpoint。
- ファイルベースレスポンス、レスポンス遅延、Docker を使ったローカル開発サポート。

## ❤️ Sponsors

SemanticStub がワークフローの役に立ったら、スポンサーをご検討ください 🙌

ご支援によって、次の取り組みを継続できます。
- SemanticStub の保守と改善
- 関連する開発者向けツールの開発
- AI 支援開発に関する調査と実験

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

### レスポンステンプレート置換

レスポンスの `example` 値に `{{source.key}}` 形式のプレースホルダーを記述すると、
実行時にリクエストの値へ置換されます。

| プレースホルダー | 参照元 |
| --- | --- |
| `{{path.name}}` | URL パスパラメータ |
| `{{query.name}}` | クエリストリングパラメータの最初の値 |
| `{{header.name}}` | リクエストヘッダーの値 |
| `{{body.key}}` | JSON ボディのトップレベルフィールド |
| `{{body.a.b.c}}` | ドット記法によるネストした JSON ボディフィールド |

例:

```yaml
paths:
  /users/{id}:
    get:
      parameters:
        - name: id
          in: path
          required: true
          schema:
            type: string
      responses:
        '200':
          content:
            application/json:
              example:
                id: "{{path.id}}"
                name: "User {{path.id}}"

  /orders:
    post:
      responses:
        '201':
          content:
            application/json:
              example:
                orderId: "{{body.order.id}}"
                customerName: "{{body.customer.profile.name}}"
```

注意事項:

- プレースホルダーが解決できない場合（キーが存在しない、中間セグメントが存在しない、
  中間セグメントが JSON オブジェクトでないなど）、プレースホルダーはそのままレスポンスに残ります。
- JSON レスポンスに埋め込まれる文字列値は自動的に JSON エスケープされます。
- `{{body.*}}` はドット記法による任意の深さのトラバースをサポートします。
  他のソース（`path`、`query`、`header`）はキー名にドット記法を使用できません。
- テンプレート置換はインラインの `example` 値にのみ適用されます。
  `x-response-file` で提供されるレスポンスは置換処理の対象外です。

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
            region:
              regex: ^ap-.*
          headers:
            X-Env:
              equals: staging
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
  /oauth/token:
    post:
      x-match:
        - body:
            form:
              grant_type:
                equals: authorization_code
              code:
                regex: "^[A-Za-z0-9_-]+$"
          response:
            statusCode: 200
            content:
              application/json:
                example:
                  access_token: token-123
      responses:
        '400':
          description: Invalid token request
```

各 `x-match` エントリには次を含められます。

- `query`: scalar の `equals` 省略形、または明示的な `equals` / `regex` operator による query string 条件
- `headers`: scalar の `equals` 省略形、または明示的な `equals` / `regex` operator による header 条件
- `body`: JSON body 条件、または `body.form` による form-urlencoded body 条件
- `x-semantic-match`: セマンティックフォールバックマッチングに使う自然言語の説明
- `response`: 条件に一致したときに返すレスポンス

補足:

- `response.statusCode` は必須で、100 から 599 までの HTTP status code である必要があります。
- `x-match` のレスポンスは、通常レスポンスと同じく `content`、`headers`、`x-delay`、`x-response-file` をサポートします。
- query、header、body 条件は、1 つの `x-match` 内では AND 条件として組み合わされます。
- `x-match` で使う query と header のキーは、path または operation にパラメータ宣言がある場合、その OpenAPI 宣言を参照している必要があります。
- scalar の `query` / `headers` 値は `equals` の省略形として扱われます。
- `query.equals` は単一値の完全一致、順序付きの繰り返し値、および `integer`、`number`、`boolean` など宣言済み OpenAPI query parameter type に対する型付き比較をサポートします。
- `query.regex` と `headers.regex` は regex マッチを行います。contains / starts-with / ends-with は `.*value.*`、`^value`、`value$` のような regex pattern で表現できます。
- JSON `body` マッチは object に対して部分一致なので、追加プロパティを含んでいても一致できます。
- `application/x-www-form-urlencoded` リクエストボディには `body.form` を使います。scalar の form 値は `equals` の省略形として扱われ、form field には明示的な `equals` / `regex` operator も使えます。設定した form key は存在する必要があり、追加の request form key は許可されます。
- `body.form` は同じ match entry 内で `body.json` や `body.text` と併用できません。
- 不正な JSON リクエストボディは `body` 条件に一致しません。
- `x-semantic-match` エントリは、すべての決定的な条件が失敗したときのみ評価されます。アプリケーション設定でセマンティックマッチングを有効化する必要があります。`x-semantic-match` を含むエントリに `query`、`headers`、`body` を同時に指定することはできません。
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
          content:
            application/json:
              example:
                message: no match
```

`appsettings.json` でセマンティックマッチングを設定します:

```json
"StubSettings": {
  "SemanticMatching": {
    "Enabled": true,
    "Endpoint": "http://localhost:8081",
    "Threshold": 0.85,
    "TopScoreMargin": 0,
    "TimeoutSeconds": 30
  }
}
```

| 設定 | 説明 | デフォルト |
| --- | --- | --- |
| `Enabled` | セマンティックマッチングフォールバックを有効化します。 | `false` |
| `Endpoint` | TEI エンドポイントのベース URL。 | `""` |
| `Threshold` | マッチを受け入れる最小コサイン類似度 (-1.0〜1.0)。 | `0.85` |
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

## Runtime inspection

SemanticStub は、runtime inspection endpoint を予約プレフィックス
`/_semanticstub/runtime` 配下に公開します。

- `GET /_semanticstub/runtime/config` は、現在有効な effective configuration snapshot のメタデータを返します。
- `GET /_semanticstub/runtime/routes` は、現在有効な正規化済み route list を返します。
- `GET /_semanticstub/runtime/routes/{routeId}` は、1 件の active route の effective runtime detail を返します。
- `GET /_semanticstub/runtime/scenarios` は、現在の scenario state snapshot を返します。
- `GET /_semanticstub/runtime/metrics` は、現在のプロセスで処理した実リクエストの集計 metrics を返します。
- `POST /_semanticstub/runtime/metrics/resets` は、現在のプロセスの集計 metrics と recent request history を reset します。互換性維持のため `POST /_semanticstub/runtime/metrics/reset` も引き続き利用できます。
- `GET /_semanticstub/runtime/requests?limit=20` は、現在のプロセスで処理した実リクエストの recent request history を返します。
- `POST /_semanticstub/runtime/test-match` は、実レスポンスを実行せず scenario state も変更せずに virtual request を評価します。
- `POST /_semanticstub/runtime/explain` は、virtual request の structured match detail を返します。該当する場合は deterministic / semantic evaluation も含みます。
- `GET /_semanticstub/runtime/explain/last` は、現在のプロセスで最後に実リクエストから記録された explanation を返します。
- `POST /_semanticstub/runtime/scenarios/resets` は、設定済みの全 scenario を初期状態に戻します。互換性維持のため `POST /_semanticstub/runtime/scenarios/reset` も引き続き利用できます。
- `POST /_semanticstub/runtime/scenarios/{name}/resets` は、設定済みの 1 scenario を初期状態に戻します。互換性維持のため `POST /_semanticstub/runtime/scenarios/{name}/reset` も引き続き利用できます。
- `GET /_semanticstub/runtime/requests/{index}/export/curl` は、記録済み実リクエストを実行可能な curl コマンドとして出力します。
- `GET /_semanticstub/runtime/requests/{index}/export/replay` は、記録済み実リクエストを構造化された replay model として出力します。
- `POST /_semanticstub/runtime/requests/{index}/replay` は、記録済み実リクエストを stub matching パイプラインで再実行し、match explanation を返します。ドライラン — レスポンスの実行も scenario state の変更も行いません。
- `GET /_semanticstub/runtime/requests/{index}/export/yaml` は、記録済み実リクエスト 1 件を OpenAPI 3.1 の YAML スタブドラフトとして出力します。
- `GET /_semanticstub/runtime/requests/export/yaml?limit=20` は、直近の記録済み実リクエストをパス・メソッド別にグループ化した YAML スタブ候補として出力します。
- `GET /_semanticstub/runtime/requests/{index}/suggest-improvements` は、記録済み実リクエストをアクティブなスタブ定義に対して分析し、YAML 改善候補を返します。
- `POST /_semanticstub/runtime/suggest-improvements` は、仮想リクエストをアクティブなスタブ定義に対して分析し、YAML 改善候補を返します。
- `/_semanticstub/runtime/*` 配下の YAML stub 定義は inspection endpoint 用に予約されており、通常の stub route としては到達できません。

補足:

- `/_semanticstub/runtime/config` はサマリ表示です。現在は snapshot timestamp、configuration hash、definitions directory、route count、semantic matching の有効状態などを返します。
- `/_semanticstub/runtime/routes` は、現在有効な path と HTTP method の組み合わせごとに 1 件ずつ、route id、正規化済み path pattern、semantic matching の利用有無、scenario の利用有無、response 数を返します。
- `/_semanticstub/runtime/routes/{routeId}` は、1 件の route について top-level response、設定済み response-file metadata、設定済み response media type、および正規化済み conditional match metadata を含む detail view を返します。`x-semantic-match` が設定されている場合は、簡潔な semantic-match summary も含まれます。
- `/_semanticstub/runtime/scenarios` は、既知の scenario ごとに現在の state と active かどうかを返します。
- `/_semanticstub/runtime/metrics` は process-local で、total request count、matched / unmatched count、fallback / semantic count、average latency、status code summary、top routes を返します。
- `/_semanticstub/runtime/metrics/resets` と `/_semanticstub/runtime/metrics/reset` は process-local な aggregate metrics と recent request history を消去します。configuration reload、scenario state の変更、`/_semanticstub/runtime/explain/last` の消去は行いません。
- `/_semanticstub/runtime/requests` は process-local で、最大 100 件の recent request history を新しい順で返します。各 item には timestamp、method、path、利用可能な場合の route id、status code、elapsed time、match mode、および unmatched request の failure reason が含まれます。`limit` query parameter のデフォルトは `20` です。
- `/_semanticstub/runtime/test-match` と `/_semanticstub/runtime/explain` は、method、path、省略可能な query / header / body、および省略可能な candidate detail flag を持つ virtual request payload を受け取ります。結果には、該当する場合に selected response の id、status code、response source（`responses` または `x-match`）、candidate index も含まれます。
- `deterministicCandidates` array の各 deterministic candidate は、match しなかった場合に `mismatchReasons` list を持ちます。各 entry には `dimension`（`query`、`header`、`body`、`scenario`、`response`）、失敗した parameter / header / JSON body path / scenario を示す `key`、stub definition の `expected`、request の `actual`（key または JSON path が存在しない場合は `null`）、および `kind`（`missing`、`unequal`、`notConfigured`）が含まれます。Body mismatch の path は `$.user.profile.role` のような bounded JSON path-style form です。Candidate が match した場合または選択された場合、この list は空です。
- semantic fallback が評価された場合、`semanticEvaluation.selectionStatus` は `selected`、`belowThreshold`、`ambiguous`、`unavailable` のいずれかです。Semantic candidate が選択されなかった場合は `nonSelectionReason` も含まれ、利用可能な場合は best candidate metadata と second-best above-threshold candidate metadata も返すため、threshold failure と low-margin ambiguity を matching behavior を変えずに比較できます。
- `/_semanticstub/runtime/explain/last` は process-local で、実リクエストが stub response に match した後だけ更新されます。
- `/_semanticstub/runtime/scenarios/resets`、`/_semanticstub/runtime/scenarios/reset`、`/_semanticstub/runtime/scenarios/{name}/resets`、`/_semanticstub/runtime/scenarios/{name}/reset` は、現在のプロセスの in-memory scenario state だけを変更します。
- `/_semanticstub/runtime/requests/{index}/export/yaml` と `/_semanticstub/runtime/requests/export/yaml` は raw YAML テキスト（`application/yaml`）を返します。出力は `TODO` プレースホルダーを含むレビュー用ドラフトで、そのままアクティブ化できる stub 定義ではありません。
- `/_semanticstub/runtime/requests/{index}/replay` は実リクエストと同じパイプラインでマッチングを評価しますが、レスポンスの実行や scenario state の変更は行いません。4096 文字を超えるボディとセンシティブなヘッダーは記録時に編集（redact）されます。
- `/_semanticstub/runtime/suggest-improvements` と `/_semanticstub/runtime/requests/{index}/suggest-improvements` は `MatchImprovementReportInfo` を返します。各改善候補は `kind`（`NoMatchFound`、`SemanticFallbackUsed`、`NoConditionsOnRoute`、`NearMissCandidate`）、人間が読める `reason`、および YAML への変更内容を示す `yamlHint` を持ちます。提案はアドバイザリーです — YAML やルーティングの動作を自動変更しません。
- これらの endpoint は、raw YAML、内部 domain object、完全な response payload body は公開しません。

`POST /_semanticstub/runtime/test-match` と
`POST /_semanticstub/runtime/explain` のリクエストボディ例:

```json
{
  "method": "GET",
  "path": "/users",
  "query": {
    "role": ["admin"]
  },
  "includeCandidates": true
}
```

Semantic fallback detail には machine-readable な比較 metadata が含まれます:

```json
{
  "semanticEvaluation": {
    "attempted": true,
    "selectionStatus": "ambiguous",
    "nonSelectionReason": "ambiguous",
    "threshold": 0.8,
    "requiredMargin": 0.03,
    "selectedScore": 0.95,
    "secondBestScore": 0.93,
    "marginToSecondBest": 0.02,
    "bestCandidateIndex": 0,
    "bestScore": 0.95,
    "secondBestCandidateIndex": 1
  }
}
```

`GET /_semanticstub/runtime/routes/listUsers` のレスポンス抜粋例:

```json
{
  "routeId": "listUsers",
  "method": "GET",
  "pathPattern": "/users",
  "usesSemanticMatching": false,
  "usesScenario": false,
  "responseCount": 1,
  "hasConditionalMatches": true,
  "responses": [
    {
      "responseId": "200",
      "delayMilliseconds": 100,
      "responseFile": "users.json",
      "mediaTypes": ["application/json"],
      "usesScenario": false,
      "scenario": null
    }
  ],
  "conditionalMatches": [
    {
      "candidateIndex": 0,
      "hasExactQuery": true,
      "exactQueryKeys": ["role"],
      "hasRegexQuery": false,
      "regexQueryKeys": [],
      "headerKeys": [],
      "hasBody": false,
      "usesSemanticMatching": false,
      "semanticMatchSummary": null,
      "responseStatusCode": 200,
      "delayMilliseconds": null,
      "responseFile": null,
      "mediaTypes": ["application/json"],
      "usesScenario": false,
      "scenario": null
    }
  ]
}
```

`GET /_semanticstub/runtime/requests?limit=1` のレスポンス例:

```json
[
  {
    "timestamp": "2026-04-08T00:00:00Z",
    "method": "GET",
    "path": "/users",
    "routeId": "listUsers",
    "statusCode": 200,
    "elapsedMilliseconds": 12.3,
    "matchMode": "exact",
    "failureReason": null
  }
]
```

## 開発

- Source: `src/`
- Tests: `tests/`
- Samples: `samples/`

設定に関する補足:

- `appsettings.json` はデフォルトの実行時設定です。
- `appsettings.Development.json` は `Development` 環境で起動したときだけ反映され、ローカルでの semantic matching 設定や、request / response body を含むより詳細な HTTP ログの確認に向いています。
- コンソールログは、trace / span の相関情報を含む single-line 形式で出力され、ローカルでのリクエスト追跡をしやすくしています。
- `SemanticStub.http` のサンプルリクエストはローカル起動を前提としており、`Development` 環境を有効にすると確認しやすいケースがあります。

サンプルファイル:

- `samples/basic-routing.yaml` は基本的な routing、matching、scenario、file response の動作例です。
- `samples/semantic-search.stub.yaml` は semantic matching の動作例で、`SemanticMatching` 設定をローカルで試すときに便利です。

## 実行

通常起動:

```sh
dotnet run --project src/SemanticStub.Api
```

`appsettings.Development.json` を反映したい場合は、`Development` 環境で起動します:

```sh
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/SemanticStub.Api
```

## Docker

Docker ベースの構成をビルドします:

```sh
docker compose build
```

SemanticStub と埋め込みサービスをバックグラウンドで起動します:

```sh
docker compose up -d
```

この構成では SemanticStub を `http://localhost:8080` で公開します。
`samples/` ディレクトリはコンテナへマウントされるため、stub YAML を編集しても
イメージの再ビルドは不要です。TEI は Docker 内部ネットワークだけで使用され、
ホストには公開しません。

Claude Desktop に登録する前に、MCP サーバーをビルドします:

```sh
cd mcp
npm install
npm run build
```

Claude Desktop には次のように MCP サーバーを追加します:

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

## Agent Skill

[Agent Skills](https://agentskills.io) 仕様に対応したスキルを使うと、MCP ツールを効率よく利用できます。Claude Code・GitHub Copilot・Cursor など多くのエージェントで利用可能です。

### gh skill で使う場合

```sh
gh skill install winuim/SemanticStub semantic-stub
```

### Claude Desktop に手動インストール

1. `skills/semantic-stub.skill` をダウンロードします
2. Claude Desktop → カスタマイズ → スキル を開きます
3. `+` ボタン → `スキルを作成` をクリックします
4. `スキルをアップロード` を選択します
5. `.skill` ファイルをアップロードします

## テスト

```sh
dotnet test
```

Cobertura 形式でカバレッジを計測する場合:

```sh
dotnet test --collect:"XPlat Code Coverage"
```

結果を固定ディレクトリに出力する場合:

```sh
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

必要に応じて ReportGenerator で Cobertura 出力から HTML レポートを生成できます:

```sh
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:"TestResults/CoverageReport" -reporttypes:Html
```

## 補足

リポジトリ固有のガイダンスは `AGENTS.md` を参照してください。

## ライセンス

このプロジェクトは MIT ライセンスの下で公開されています。詳細は [LICENSE](LICENSE) ファイルをご覧ください。
