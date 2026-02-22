# .NET으로 AI Agent 만들기 (2편) — RAG부터 미들웨어까지

> **시리즈 구성**
> - **1편**: 환경 설정 · 첫 Agent · Function Calling · 메모리와 세션 (1~4장)
> - **2편 (이 글)**: RAG · 멀티 에이전트 · 미들웨어 & 관측성 (5~7장)

1편에서는 `AIAgent`를 만들고, Tool을 붙이고, 세션으로 대화 맥락을 유지하는 법을 익혔습니다. 2편에서는 프로덕션에서 자주 마주치는 세 가지 주제를 다룹니다. LLM이 모르는 지식을 문서에서 꺼내오는 **RAG**, 복잡한 작업을 역할별로 분리하는 **멀티 에이전트**, 그리고 안전성과 가시성을 책임지는 **미들웨어 & 관측성**입니다.

> **버전**: `Microsoft.Agents.AI.OpenAI` `1.0.0-preview.260212.1` / .NET 10 기준.  
> Public preview 단계이므로 API가 GA 전에 변경될 수 있습니다.

---

## 5장. RAG — 내 문서로 답하는 Agent

### LLM의 지식 한계를 넘는 법

LLM은 학습 데이터에 없는 사내 문서, 최신 정보, 도메인 특화 지식을 알지 못합니다. 모른다고 솔직히 말하면 다행이지만, 그럴듯하게 꾸며낸 답변(할루시네이션)을 돌려줄 때가 더 문제입니다.

**RAG(Retrieval-Augmented Generation)** 는 이 문제를 "검색 + 생성" 이중 구조로 해결합니다. 질문을 벡터로 변환해 문서 인덱스에서 관련 청크를 찾고, 그것을 프롬프트에 끼워 넣어 LLM에게 근거 자료로 제공합니다.

```
사용자 질문
    │
    ▼
[임베딩] 질문 → 벡터
    │
    ▼
[검색] 벡터 DB에서 유사 청크 Top-K 조회
    │
    ▼
[주입] 프롬프트에 청크 포함
    │
    ▼
[생성] LLM이 컨텍스트 기반 답변 생성
```

핵심 개념 네 가지를 짚고 넘어가겠습니다.

| 개념 | 설명 |
|------|------|
| **임베딩(Embedding)** | 텍스트를 고차원 숫자 벡터로 변환. 의미적으로 유사한 텍스트는 벡터 공간에서 가깝게 위치 |
| **청킹(Chunking)** | 긴 문서를 LLM 컨텍스트 창에 맞게 작은 단위로 분할 |
| **벡터 검색** | 질문 벡터와 문서 벡터 간 코사인 유사도를 계산해 가장 관련성 높은 청크 선택 |
| **컨텍스트 주입** | 검색된 청크를 프롬프트에 포함시켜 LLM에게 근거 자료로 제공 |

### 설정: 임베딩 배포명 추가

`appsettings.local.json`에 임베딩 모델 배포명을 추가합니다.

```json
{
  "AzureOpenAI": {
    "Endpoint": "https://<your-resource>.openai.azure.com/openai/v1/",
    "ApiKey": "<your-api-key>",
    "DeploymentName": "gpt-4o-mini",
    "EmbeddingDeploymentName": "text-embedding-3-small"
  }
}
```

패키지 추가 없이 기존 `Microsoft.Extensions.AI.OpenAI`의 `AsIEmbeddingGenerator()` 확장 메서드를 사용합니다.

### 임베딩 생성

`AzureOpenAIClient`에서 `EmbeddingClient`를 꺼내 `IEmbeddingGenerator<string, Embedding<float>>`로 변환합니다.

```csharp
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure;

var azureClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey));

IEmbeddingGenerator<string, Embedding<float>> embedder =
    azureClient
        .GetEmbeddingClient("text-embedding-3-small")
        .AsIEmbeddingGenerator();

var results = await embedder.GenerateAsync(["안녕하세요, Contoso입니다."]);
float[] vector = results[0].Vector.ToArray();  // 1536차원 float 배열
```

### 인메모리 벡터 인덱스

이 샘플에서는 외부 벡터 DB 없이 `List<IndexedChunk>`에 벡터를 저장하고 코사인 유사도로 직접 검색합니다. 구조는 단순합니다.

```csharp
private sealed record IndexedChunk(
    string Title,
    string Text,
    float[] Embedding);

static float CosineSimilarity(float[] a, float[] b)
{
    float dot = 0f, normA = 0f, normB = 0f;
    for (int i = 0; i < a.Length; i++)
    {
        dot   += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB) + 1e-10f);
}
```

> **프로덕션 선택지**: 문서가 수천 건 이상이면 외부 벡터 DB가 필요합니다. Azure 환경이라면 **Azure AI Search**, 오픈소스를 선호한다면 **Qdrant**, 기존 PostgreSQL을 활용하려면 **pgvector**를 고려하세요.

### 색인 → 검색 → 생성 파이프라인

**색인** 단계에서는 각 문서 청크를 임베딩해 인덱스에 추가합니다.

```csharp
var index = new List<IndexedChunk>();

foreach (var (title, text) in documents)
{
    var result = await embedder.GenerateAsync([text]);
    index.Add(new IndexedChunk(title, text, result[0].Vector.ToArray()));
}
```

**검색** 단계에서는 질문을 임베딩한 뒤 코사인 유사도 Top-K를 뽑습니다.

```csharp
async Task<List<string>> RetrieveAsync(string query, int topK)
{
    var result = await embedder.GenerateAsync([query]);
    var queryVector = result[0].Vector.ToArray();

    return index
        .Select(chunk => (chunk, score: CosineSimilarity(queryVector, chunk.Embedding)))
        .OrderByDescending(x => x.score)
        .Take(topK)
        .Select(x => $"[{x.chunk.Title}] {x.chunk.Text}")
        .ToList();
}
```

**생성** 단계에서는 검색된 청크를 프롬프트에 주입해 Agent를 호출합니다.

```csharp
var contextChunks = await RetrieveAsync(question, topK: 3);
var contextBlock  = string.Join("\n\n", contextChunks);

var prompt =
    $"""
    === 참고 문서 ===
    {contextBlock}

    === 질문 ===
    {question}
    """;

await foreach (var update in agent.RunStreamingAsync(prompt, session: null))
    Console.Write(update.Text);
```

Agent의 `instructions`에 **컨텍스트만 근거로 답변**하도록 명시하면 할루시네이션을 억제할 수 있습니다.

```csharp
AIAgent agent = azureClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions:
            "당신은 Contoso 고객 지원 에이전트입니다. " +
            "반드시 제공된 컨텍스트(문서 발췌)만을 근거로 대답하세요. " +
            "컨텍스트에 없는 내용은 '해당 정보를 찾을 수 없습니다'라고 답하세요.",
        name: "RAGAgent"
    );
```

### 실행

```bash
dotnet run -- 04
```

```
=== 04: RAG (Retrieval-Augmented Generation) ===

[1/3] 문서 색인 중...
  색인 완료: [회사 소개]
  색인 완료: [제품: CloudERP Pro]
  ...

[2/3] RAG 질의응답 시작...
Q: Contoso는 언제, 어디서 설립되었나요?
A: Contoso Inc.는 2010년 서울에서 설립되었습니다.

Q: CloudERP Pro의 SLA는 몇 퍼센트인가요?
A: CloudERP Pro는 99.9% SLA를 보장합니다.
```

전체 코드는 `samples/04_RAG.cs`를 참조하세요.

### RAG 품질 개선 팁

인덱스를 만들었다고 끝이 아닙니다. 검색 품질을 높이는 방법은 다양합니다.

| 기법 | 설명 |
|------|------|
| **청크 크기 조정** | 너무 크면 노이즈 증가, 너무 작으면 컨텍스트 부족. 512~1024 토큰이 일반적 |
| **청크 오버랩** | 청크 경계에서 문맥이 끊기지 않도록 앞뒤 청크를 일부 겹침 |
| **하이브리드 검색** | 벡터 검색 + 키워드 검색(BM25) 결합으로 정확도 향상 |
| **Re-ranking** | Top-K 결과를 Cross-Encoder 모델로 재정렬 |
| **메타데이터 필터링** | 검색 전 날짜·카테고리 등으로 후보를 좁혀 관련성 향상 |

---

## 6장. 멀티 에이전트 — 역할 분리로 복잡성 정복

### 왜 에이전트를 여럿으로 나누는가?

단일 Agent는 모든 역할을 혼자 처리합니다. 기능이 늘어날수록 `instructions`는 길어지고, LLM은 무엇을 해야 할지 혼란스러워합니다. **멀티 에이전트**는 이 문제를 역할 분리로 해결합니다. 날씨는 날씨 전문가에게, 계산은 계산 전문가에게 맡기고, 오케스트레이터는 라우팅만 담당합니다.

```
사용자 질문
    │
    ▼
[OrchestratorAgent]   ← 의도 파악 → 라우팅
    │
    ├─▶ [WeatherAgent]     날씨 전문
    ├─▶ [CalculatorAgent]  계산 전문
    └─▶ (직접 답변)         일반 질문
```

### 구현 패턴: 전문 에이전트를 Tool로 래핑

Microsoft Agent Framework에서 가장 자연스러운 멀티 에이전트 구현 방법은 **전문 에이전트를 `AIFunctionFactory`로 Tool화**하여 오케스트레이터에 등록하는 것입니다. 오케스트레이터는 LLM의 Function Calling 능력으로 적절한 Tool을 자동 선택·호출합니다.

**1단계: 전문 에이전트 생성**

각 전문 에이전트는 범위가 좁은 `instructions`를 갖는 일반 `AIAgent`입니다.

```csharp
AIAgent weatherAgent = azureClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions:
            "당신은 날씨 전문 에이전트입니다. " +
            "날씨 관련 질문에만 답변하며, 간결하게 답합니다.",
        name: "WeatherAgent"
    );

AIAgent calculatorAgent = azureClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions:
            "당신은 수학 계산 전문 에이전트입니다. " +
            "수식과 계산 문제를 정확하게 풀어 결과만 간결하게 답합니다.",
        name: "CalculatorAgent"
    );
```

**2단계: 전문 에이전트를 Tool로 래핑**

`description`은 LLM이 어떤 Tool을 선택할지 판단하는 핵심 근거입니다. **명확하고 구체적으로** 작성하세요.

```csharp
var weatherTool = AIFunctionFactory.Create(
    async (string question) =>
    {
        var response = await weatherAgent.RunAsync(question, session: null);
        return response.Text ?? string.Empty;
    },
    name: "ask_weather_agent",
    description: "날씨, 기온, 강수, 미세먼지 등 기상 관련 질문을 날씨 전문 에이전트에게 전달합니다.");

var calculatorTool = AIFunctionFactory.Create(
    async (string expression) =>
    {
        var response = await calculatorAgent.RunAsync(expression, session: null);
        return response.Text ?? string.Empty;
    },
    name: "ask_calculator_agent",
    description: "수학 계산, 수식 풀이, 단위 변환 등 계산 관련 질문을 계산기 전문 에이전트에게 전달합니다.");
```

**3단계: 오케스트레이터 에이전트 구성**

전문 에이전트 Tool들을 `tools:` 파라미터에 넘겨 오케스트레이터를 만듭니다.

```csharp
AIAgent orchestrator = azureClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions:
            "당신은 사용자 요청을 적절한 전문 에이전트에게 위임하는 오케스트레이터입니다. " +
            "날씨 관련 질문 → ask_weather_agent, " +
            "수학/계산 관련 질문 → ask_calculator_agent, " +
            "그 외 일반 질문 → 직접 답변. " +
            "항상 한국어로 최종 답변을 제공하세요.",
        name: "OrchestratorAgent",
        tools: [weatherTool, calculatorTool]
    );
```

사용자는 오케스트레이터 하나만 알면 됩니다. 라우팅은 내부에서 자동으로 이루어집니다.

```csharp
await foreach (var update in orchestrator.RunStreamingAsync(question, session: null))
    Console.Write(update.Text);
```

복합 질문도 문제없습니다. LLM은 `ask_weather_agent`와 `ask_calculator_agent`를 동시에 호출하기로 결정하고 두 에이전트를 병렬로 실행합니다.

```
사용자: "부산 내일 비 올 확률이랑 27 더하기 38은?"
  → 오케스트레이터 LLM: ask_weather_agent + ask_calculator_agent 동시 호출 결정
  → 두 에이전트 병렬 실행 → 결과 취합
  → 오케스트레이터가 통합 답변 생성
```

### 실행

```bash
dotnet run -- 05
```

```
=== 05: 멀티 에이전트 협업 ===

사용자: 오늘 서울 날씨가 어때?
  [WeatherAgent 호출] 질문: 오늘 서울 날씨가 어때?
최종 답변: 오늘 서울은 맑고 기온은 22°C입니다.

사용자: 123 곱하기 456은 얼마야?
  [CalculatorAgent 호출] 식: 123 곱하기 456은 얼마야?
최종 답변: 123 × 456 = 56,088입니다.
```

전체 코드는 `samples/05_MultiAgent.cs`를 참조하세요.

### 멀티 에이전트 설계 팁

| 고려 사항 | 설명 |
|-----------|------|
| **에이전트 경계** | 각 에이전트의 역할을 명확히 분리. 겹치는 영역이 많으면 오케스트레이터가 혼란 |
| **Tool description** | LLM이 라우팅 판단에 사용하므로 구체적으로 작성 |
| **에러 처리** | 전문 에이전트 실패 시 오케스트레이터가 대체 응답을 제공할 수 있도록 |
| **컨텍스트 공유** | 세션을 에이전트 간에 공유할지, 독립적으로 유지할지 설계 필요 |
| **비용** | 호출마다 LLM API 비용 발생. 간단한 라우팅은 규칙 기반으로 처리도 고려 |

---

## 7장. 미들웨어 & 관측성 — 안전하고 투명한 Agent

### 미들웨어란?

**미들웨어**는 Agent의 요청·응답 파이프라인에 끼어드는 처리 단계입니다. ASP.NET Core의 HTTP 미들웨어와 동일한 개념입니다. 입력이 LLM에 도달하기 전, 그리고 응답이 사용자에게 돌아오기 전에 원하는 로직을 삽입할 수 있습니다.

```
[사용자 입력]
     │
     ▼
[LoggingMiddleware]   ← 요청 시작 시간 기록
     │
     ▼
[GuardrailMiddleware] ← 금지 콘텐츠 차단
     │
     ▼
[PiiMiddleware]       ← 개인정보 마스킹
     │
     ▼
[AIAgent 핵심 로직]   ← LLM 호출
     │
     ▼ (응답이 역방향으로 올라옴)
[PiiMiddleware]       ← 응답의 개인정보 마스킹
     │
     ▼
[GuardrailMiddleware] ← 응답의 금지 콘텐츠 차단
     │
     ▼
[LoggingMiddleware]   ← 응답 시간 및 길이 기록
     │
     ▼
[최종 응답]
```

미들웨어로 할 수 있는 것들을 정리하면 다음과 같습니다.

| 용도 | 설명 |
|---|---|
| 로깅 | 요청/응답 내용, 처리 시간, 토큰 수 기록 |
| 가드레일 | 금지 키워드·유해 콘텐츠 탐지 및 차단 |
| PII 마스킹 | 이메일·전화번호·이름 등 개인정보 자동 제거 |
| 재시도 | 네트워크 오류 시 지수 백오프로 재시도 |
| 캐싱 | 동일 입력에 대한 응답 캐싱 |
| 레이트 리밋 | 호출 빈도 제한 |

### 미들웨어 함수 서명

미들웨어는 클래스가 아닌 **함수**로 정의합니다.

```csharp
Task<AgentResponse> MiddlewareFunc(
    IEnumerable<ChatMessage> messages,   // 입력 메시지
    AgentSession?            session,    // 세션 (null 가능)
    AgentRunOptions?         options,    // 실행 옵션 (null 가능)
    AIAgent                  innerAgent, // 다음 단계 Agent
    CancellationToken        ct)
```

`innerAgent.RunAsync(messages, session, options, ct)`를 호출하면 파이프라인의 다음 단계로 진행합니다. 이 호출 전후로 로직을 삽입하면 됩니다.

> **타입 충돌 주의**: `IEnumerable<ChatMessage>`의 `ChatMessage`는 `Microsoft.Extensions.AI.ChatMessage`입니다.  
> 프로젝트에 `using OpenAI.Chat;`이 함께 있으면 타입이 모호해집니다. alias로 해결하세요.
>
> ```csharp
> using MEAIChatMessage = Microsoft.Extensions.AI.ChatMessage;
> ```

### 미들웨어 등록: `AsBuilder().Use().Build()`

```csharp
var agent = baseAgent
    .AsBuilder()
    .Use(PiiMiddleware,       null)  // 가장 안쪽 (마지막 실행)
    .Use(GuardrailMiddleware, null)  // 중간
    .Use(LoggingMiddleware,   null)  // 가장 바깥쪽 (가장 먼저 실행)
    .Build();
```

`Use()`의 두 번째 인자는 스트리밍 미들웨어 함수입니다. 스트리밍이 필요 없으면 `null`을 전달합니다.

**실행 순서 규칙**: `.Use()`로 나중에 등록할수록 바깥쪽(먼저 실행)에 위치합니다. 위 예시의 실제 실행 순서는 `Logging → Guardrail → PII → Agent → PII → Guardrail → Logging`입니다.

### 미들웨어 구현 예제

**1. 로깅 미들웨어** — 요청·응답 시각과 응답 길이를 기록합니다.

```csharp
private static async Task<AgentResponse> LoggingMiddleware(
    IEnumerable<MEAIChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    var inputText = string.Join(" | ",
        messages.Select(m => m.Text?.Substring(0, Math.Min(m.Text.Length, 30))));
    Console.WriteLine($"[Logging] 요청 시작 | 입력: \"{inputText}...\"");

    var sw = Stopwatch.StartNew();
    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);
    sw.Stop();

    Console.WriteLine($"[Logging] 요청 완료 | {sw.ElapsedMilliseconds}ms | {response.Text?.Length ?? 0}자");
    return response;
}
```

**2. 가드레일 미들웨어** — 금지 키워드가 포함된 입력·출력을 차단합니다.

```csharp
private static readonly string[] ForbiddenKeywords =
    ["해로운", "harmful", "illegal", "violence", "폭력"];

private static async Task<AgentResponse> GuardrailMiddleware(
    IEnumerable<MEAIChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    var filtered = messages
        .Select(m => new MEAIChatMessage(m.Role, Redact(m.Text ?? "")))
        .ToList();

    var response = await innerAgent.RunAsync(filtered, session, options, cancellationToken);

    response.Messages = response.Messages
        .Select(m => new MEAIChatMessage(m.Role, Redact(m.Text ?? "")))
        .ToList();

    return response;
}

private static string Redact(string content)
{
    foreach (var kw in ForbiddenKeywords)
        if (content.Contains(kw, StringComparison.OrdinalIgnoreCase))
            return "[차단됨: 허용되지 않는 콘텐츠]";
    return content;
}
```

**3. PII 마스킹 미들웨어** — 정규식으로 이메일, 전화번호, 한국어 이름을 탐지해 대체합니다.

```csharp
private static readonly Regex[] PiiPatterns =
[
    new(@"\b\d{2,3}-\d{3,4}-\d{4}\b",              RegexOptions.Compiled), // 전화번호
    new(@"\b[\w\.\-]+@[\w\.\-]+\.\w{2,}\b",         RegexOptions.Compiled), // 이메일
    new(@"\b[가-힣]{2,4}(?=\s*(씨|님|이|가|은|는))", RegexOptions.Compiled), // 한국어 이름
];

private static async Task<AgentResponse> PiiMiddleware(
    IEnumerable<MEAIChatMessage> messages,
    AgentSession? session,
    AgentRunOptions? options,
    AIAgent innerAgent,
    CancellationToken cancellationToken)
{
    var filtered = messages
        .Select(m => new MEAIChatMessage(m.Role, MaskPii(m.Text ?? "")))
        .ToList();

    var response = await innerAgent.RunAsync(filtered, session, options, cancellationToken);

    response.Messages = response.Messages
        .Select(m => new MEAIChatMessage(m.Role, MaskPii(m.Text ?? "")))
        .ToList();

    return response;
}

private static string MaskPii(string content)
{
    foreach (var pattern in PiiPatterns)
        content = pattern.Replace(content, "[개인정보 삭제]");
    return content;
}
```

### OpenTelemetry 연동

Agent Framework는 내부적으로 OpenTelemetry `ActivitySource`를 방출합니다. `Sdk.CreateTracerProviderBuilder()`로 이를 수집해 원하는 대상으로 내보낼 수 있습니다.

```xml
<!-- AgentSamples.csproj에 이미 포함 -->
<PackageReference Include="OpenTelemetry"                  Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.*" />
```

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("AgentSamples.Middleware")  // 커스텀 ActivitySource
    .AddSource("*Microsoft.Agents.AI")    // Agent Framework 내장 Telemetry
    .AddConsoleExporter()                 // 개발용 콘솔 출력
    .Build();
```

`tracerProvider`는 `IDisposable`이므로 `using`으로 선언합니다. `Build()` 이후 생성된 모든 Activity가 자동으로 수집됩니다.

프로덕션에서는 Exporter만 교체하면 됩니다.

```csharp
// Jaeger / OpenTelemetry Collector
.AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317"))

// Azure Monitor (Application Insights)
.AddAzureMonitorTraceExporter(opt => opt.ConnectionString = "InstrumentationKey=...")
```

Agent Framework가 자동으로 방출하는 주요 Span 속성은 다음과 같습니다.

| 속성 | 설명 |
|---|---|
| `agent.name` | Agent 이름 (`AsAIAgent(name: ...)` 에서 설정) |
| `llm.request.model` | 사용된 배포 이름 |
| `llm.usage.prompt_tokens` | 입력 토큰 수 |
| `llm.usage.completion_tokens` | 출력 토큰 수 |
| `tool.name` | 호출된 Tool 이름 |
| `error.type` | 오류 발생 시 예외 타입 |

비즈니스 로직에 커스텀 Span을 직접 추가할 수도 있습니다.

```csharp
private static readonly ActivitySource _activitySource = new("AgentSamples.Middleware");

using (var activity = _activitySource.StartActivity("Example1_Logging"))
{
    activity?.SetTag("example", "logging");
    activity?.SetTag("user.id", "user-123");

    var response = await agent.RunAsync("질문 내용");

    activity?.SetTag("response.length", response.Text?.Length ?? 0);
}
```

`StartActivity()`는 `tracerProvider`에 해당 `ActivitySource`가 등록되어 있을 때만 실제 Span을 생성합니다. 등록되지 않은 경우 `null`을 반환하므로 `?.`를 항상 사용하세요.

### 내장 미들웨어 확장 메서드

자주 쓰이는 미들웨어는 빌더 확장 메서드로 이미 제공됩니다.

```csharp
var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

var agent = baseAgent
    .AsBuilder()
    .Use(PiiMiddleware, null)     // 커스텀 미들웨어
    .UseLogging(loggerFactory)    // 내장 로깅 미들웨어
    .UseOpenTelemetry()           // 내장 OpenTelemetry 미들웨어
    .Build();
```

### 실행

```bash
dotnet run -- 06
```

```
=== 06: 미들웨어 & 관측성 ===

=== 예시 1: 로깅 미들웨어 (처리 시간 측정) ===
[Logging] 요청 시작 | 입력: "대한민국의 수도는 어디인가요?..."
[Guardrail] 입력 메시지 검사 완료
[PII] 입력 메시지 PII 마스킹 완료
[PII] 출력 메시지 PII 마스킹 완료
[Guardrail] 출력 메시지 검사 완료
[Logging] 요청 완료 | 23ms | 42자
응답: 대한민국의 수도는 서울입니다.

=== 예시 3: PII 필터링 미들웨어 (개인정보 마스킹) ===
입력 (마스킹 전): 내 이름은 홍길동이고 이메일은 hong@example.com, 전화는 010-1234-5678입니다.
...
```

전체 코드는 `samples/06_Middleware.cs`를 참조하세요.

---

## 마무리 — 시리즈를 마치며

두 편에 걸쳐 .NET + Microsoft Agent Framework로 AI Agent를 만드는 전 과정을 살펴봤습니다.

**1편에서 배운 것:**
- `AsAIAgent()`로 첫 번째 Agent를 만들고 스트리밍 응답 받기
- `AIFunctionFactory.Create()`로 Tool을 붙여 Function Calling 구현
- `AgentSession`으로 멀티턴 대화 컨텍스트 유지

**2편에서 배운 것:**
- `IEmbeddingGenerator` + 코사인 유사도로 인메모리 RAG 파이프라인 구성
- 전문 에이전트를 Tool로 래핑해 오케스트레이터 패턴 구현
- `AsBuilder().Use().Build()`로 로깅·가드레일·PII 미들웨어 체인 구성
- OpenTelemetry로 Agent 내부 동작을 추적하고 관측

**전체 샘플 코드 위치:**

```
samples/
├── 01_HelloAgent.cs      ← 1장: 첫 번째 Agent
├── 02_AddTools.cs        ← 3장: Function Calling
├── 03_MultiTurn.cs       ← 4장: 메모리와 세션
├── 04_RAG.cs             ← 5장: RAG
├── 05_MultiAgent.cs      ← 6장: 멀티 에이전트
└── 06_Middleware.cs      ← 7장: 미들웨어 & 관측성
```

모든 샘플은 `dotnet run -- <번호>`로 실행할 수 있습니다.

**다음 단계:**

이 시리즈는 `1.0.0-preview.260212.1` 기준으로 작성되었습니다. 프레임워크가 GA에 가까워질수록 API가 안정화될 것입니다. 그 전에 프로덕션 적용을 고려한다면 다음을 염두에 두세요.

- **외부 벡터 DB 연동**: Azure AI Search, Qdrant 등으로 인메모리 인덱스를 교체
- **Durable Task 통합**: 장시간 실행되는 Agent 워크플로를 내구성 있게 운영
- **`AgentWorkflowBuilder`**: Graph 기반 워크플로로 복잡한 에이전트 협업 정의
- **Azure Container Apps**: Agent를 서버리스로 배포하고 자동 스케일링 활용

Microsoft Agent Framework의 공식 저장소와 릴리스 노트를 지속적으로 확인하며 GA 시점을 대비하세요.
