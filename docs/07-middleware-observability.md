# 7장. 미들웨어 & 관측성

> **주의**: Microsoft Agent Framework는 현재 **public preview** (`1.0.0-preview.260212.1`) 상태입니다. API는 정식 출시 전에 변경될 수 있습니다.

## 미들웨어란?

**미들웨어**는 Agent의 요청·응답 파이프라인에 끼어드는 처리 단계입니다.  
ASP.NET Core의 HTTP 미들웨어와 동일한 개념을 Agent에 적용한 것입니다.

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

미들웨어로 할 수 있는 것:

| 용도 | 설명 |
|---|---|
| 로깅 | 요청/응답 내용, 처리 시간, 토큰 수 기록 |
| 가드레일 | 금지 키워드·유해 콘텐츠 탐지 및 차단 |
| PII 마스킹 | 이메일·전화번호·이름 등 개인정보 자동 제거 |
| 재시도 | 네트워크 오류 시 지수 백오프로 재시도 |
| 캐싱 | 동일 입력에 대한 응답 캐싱 |
| 레이트 리밋 | 호출 빈도 제한 |

---

## 미들웨어 API

### 미들웨어 함수 서명

미들웨어는 특정 인터페이스를 구현하는 클래스가 아니라 **함수**로 정의합니다.

```csharp
// 미들웨어 함수 서명
Task<AgentResponse> MiddlewareFunc(
    IEnumerable<ChatMessage> messages,   // 입력 메시지
    AgentSession?            session,    // 세션 (null 가능)
    AgentRunOptions?         options,    // 실행 옵션 (null 가능)
    AIAgent                  innerAgent, // 다음 단계 Agent
    CancellationToken        ct)
```

`innerAgent.RunAsync(messages, session, options, ct)`를 호출하면 파이프라인의 다음 단계로 진행합니다.  
이 호출 전후로 로직을 삽입하면 됩니다.

> **주의**: `IEnumerable<ChatMessage>`의 `ChatMessage`는 `Microsoft.Extensions.AI.ChatMessage`입니다.  
> 프로젝트에 `using OpenAI.Chat;`이 함께 있으면 타입이 모호해지므로 `using` alias를 사용하세요.
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

`Use()`의 두 번째 인자는 스트리밍 미들웨어 함수입니다. 스트리밍을 사용하지 않으면 `null`을 전달합니다.

**실행 순서 규칙**: `.Use()`로 나중에 등록할수록 바깥쪽(먼저 실행)에 위치합니다.  
위 예시에서 실제 실행 순서는 `Logging → Guardrail → PII → Agent → PII → Guardrail → Logging`입니다.

---

## 미들웨어 구현 예제

### 1. 로깅 미들웨어

요청 시작·종료 시각과 응답 길이를 콘솔에 출력합니다.

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

    // ↓ 다음 단계(혹은 실제 Agent)로 제어를 넘김
    AgentResponse response = await innerAgent.RunAsync(messages, session, options, cancellationToken);

    sw.Stop();
    Console.WriteLine($"[Logging] 요청 완료 | {sw.ElapsedMilliseconds}ms | {response.Text?.Length ?? 0}자");

    return response;
}
```

### 2. 가드레일 미들웨어

금지 키워드가 포함된 입력·출력을 차단합니다.

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
    // 입력 검사: 금지어가 있으면 내용을 대체 문자로 교체
    var filtered = messages
        .Select(m => new MEAIChatMessage(m.Role, Redact(m.Text ?? "")))
        .ToList();

    var response = await innerAgent.RunAsync(filtered, session, options, cancellationToken);

    // 출력 검사
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

### 3. PII 마스킹 미들웨어

정규식으로 이메일, 전화번호, 한국어 이름 패턴을 탐지하고 대체합니다.

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

---

## OpenTelemetry 연동

Agent Framework는 내부적으로 OpenTelemetry `ActivitySource`를 방출합니다.  
`Sdk.CreateTracerProviderBuilder()`로 이를 수집하여 원하는 대상으로 내보낼 수 있습니다.

### 패키지

`AgentSamples.csproj`에 이미 포함되어 있습니다:

```xml
<PackageReference Include="OpenTelemetry"                Version="1.*" />
<PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.*" />
```

### 기본 설정

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("AgentSamples.Middleware")  // 커스텀 ActivitySource
    .AddSource("*Microsoft.Agents.AI")    // Agent Framework 내장 Telemetry
    .AddConsoleExporter()                 // 개발용 콘솔 출력
    .Build();
```

`tracerProvider`는 `IDisposable`이므로 `using`으로 선언합니다.  
`Build()` 호출 이후 생성된 모든 Activity가 자동으로 수집됩니다.

### 프로덕션 Exporter 교체

개발 단계에서는 `AddConsoleExporter()`로 콘솔에 출력하고,  
프로덕션에서는 Exporter만 교체하면 됩니다:

```csharp
// Jaeger / OpenTelemetry Collector
.AddOtlpExporter(opt => opt.Endpoint = new Uri("http://localhost:4317"))

// Azure Monitor (Application Insights)
.AddAzureMonitorTraceExporter(opt => opt.ConnectionString = "InstrumentationKey=...")
```

### 추적되는 정보

Agent Framework가 자동으로 방출하는 Span 속성:

| 속성 | 설명 |
|---|---|
| `agent.name` | Agent 이름 (`AsAIAgent(name: ...)` 에서 설정) |
| `llm.request.model` | 사용된 배포 이름 |
| `llm.usage.prompt_tokens` | 입력 토큰 수 |
| `llm.usage.completion_tokens` | 출력 토큰 수 |
| `tool.name` | 호출된 Tool 이름 |
| `error.type` | 오류 발생 시 예외 타입 |

---

## 커스텀 Span 추가

비즈니스 로직에 직접 OpenTelemetry Span을 추가하면 더 세밀한 추적이 가능합니다.

```csharp
// ActivitySource 선언 (클래스 필드)
private static readonly ActivitySource _activitySource = new("AgentSamples.Middleware");

// 사용 예시
using (var activity = _activitySource.StartActivity("Example1_Logging"))
{
    activity?.SetTag("example", "logging");
    activity?.SetTag("user.id", "user-123");

    var response = await agent.RunAsync("질문 내용");

    activity?.SetTag("response.length", response.Text?.Length ?? 0);
}
```

`StartActivity()`는 `tracerProvider`에 해당 `ActivitySource`가 등록되어 있을 때만 실제 Span을 생성합니다.  
등록되지 않은 경우 `null`을 반환하므로 `?.`(null 조건 연산자)를 항상 사용하세요.

---

## 전체 코드 예제

전체 구현은 [`samples/06_Middleware.cs`](../samples/06_Middleware.cs)를 참조하세요.

### Agent 생성 및 미들웨어 체인 구성

```csharp
// 1. 기본 Agent 생성
var baseAgent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "당신은 친절한 AI 어시스턴트입니다. 항상 한국어로 답하세요.",
        name: "MiddlewareAgent"
    );

// 2. 미들웨어 체인 구성
var agent = baseAgent
    .AsBuilder()
    .Use(PiiMiddleware,       null)  // 가장 안쪽
    .Use(GuardrailMiddleware, null)
    .Use(LoggingMiddleware,   null)  // 가장 바깥쪽
    .Build();
```

### 실행

```bash
dotnet run --project samples/AgentSamples.csproj -- 07
```

예상 출력:

```
=== 07: 미들웨어 & 관측성 ===

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

---

## 내장 미들웨어 확장 메서드

Agent Framework는 자주 쓰이는 미들웨어를 빌더 확장 메서드로 제공합니다.

### `UseLogging()`

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(b => b.AddConsole());

var agent = baseAgent
    .AsBuilder()
    .UseLogging(loggerFactory)  // 내장 로깅 미들웨어
    .Build();
```

### `UseOpenTelemetry()`

```csharp
var agent = baseAgent
    .AsBuilder()
    .UseOpenTelemetry()  // 내장 OpenTelemetry 미들웨어
    .Build();
```

커스텀 미들웨어 함수와 내장 확장 메서드를 조합할 수도 있습니다:

```csharp
var agent = baseAgent
    .AsBuilder()
    .Use(PiiMiddleware, null)
    .UseLogging(loggerFactory)
    .UseOpenTelemetry()
    .Build();
```

---

## 이 장에서 배운 것

- `AsBuilder().Use(fn).Build()` 체인으로 미들웨어를 레이어로 쌓는 방법
- 미들웨어 함수 서명: `(messages, session, options, innerAgent, ct) → Task<AgentResponse>`
- `innerAgent.RunAsync()`를 경계로 요청 전처리와 응답 후처리를 분리하는 패턴
- OpenTelemetry `TracerProvider`와 커스텀 `ActivitySource`로 세밀한 추적 구현
- `UseLogging()`, `UseOpenTelemetry()` 내장 확장 메서드 활용

---

[← 6장. 멀티 에이전트 협업](06-multi-agent.md)
