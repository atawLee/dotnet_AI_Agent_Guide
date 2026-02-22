# .NET으로 AI Agent 만들기 (1편) — 환경 설정부터 멀티턴 대화까지

> **시리즈 구성**
> - **1편 (이 글)**: 환경 설정 · 첫 Agent · Function Calling · 메모리와 세션 (1~4장)
> - [**2편**: RAG · 멀티 에이전트 · 미들웨어 & 관측성 (5~7장)](./post02.md)

Microsoft는 2024년 말 `.NET + Python` 양쪽을 지원하는 AI Agent 프레임워크를 public preview로 공개했습니다. Semantic Kernel의 엔터프라이즈 기능과 AutoGen의 직관적인 Agent 추상화를 통합한 후속 프레임워크입니다. 이 시리즈에서는 `Microsoft.Agents.AI.OpenAI` 패키지를 사용해 .NET 10 / C# 환경에서 Agent를 처음부터 만들어봅니다.

> **버전**: `Microsoft.Agents.AI.OpenAI` `1.0.0-preview.260212.1` / .NET 10 기준.  
> Public preview 단계이므로 API가 GA 전에 변경될 수 있습니다.

---

## 1장. 소개 및 환경 설정

### Microsoft Agent Framework란?

`microsoft/agent-framework`는 기존 두 프레임워크의 장점을 통합합니다.

| 출처 | 가져온 개념 |
|---|---|
| **AutoGen** | 단순하고 직관적인 Agent 추상화, 멀티 에이전트 협력 패턴 |
| **Semantic Kernel** | 엔터프라이즈급 기능 (세션 상태, 타입 안전성, 미들웨어, 텔레메트리) |

여기에 Graph 기반 Workflow(`AgentWorkflowBuilder`), Durable Task 통합, `AsAIFunction()`을 통한 통합된 멀티 에이전트 패턴이 새롭게 추가되었습니다.

**다른 프레임워크와 비교하면:**

| 기능 | Semantic Kernel | AutoGen | **Agent Framework** |
|---|---|---|---|
| Agent 추상화 | `ChatCompletionAgent` | `AssistantAgent` | **`AIAgent`** |
| Tool / Function | `KernelFunction` | `FunctionTool` | **`AIFunctionFactory`** |
| 멀티 에이전트 | `AgentGroupChat` | `GroupChat` | **`Workflow` + `AsAIFunction`** |
| 상태 관리 | `ChatHistory` 직접 관리 | 내부 메시지 히스토리 | **`AgentSession`** |
| 미들웨어 | Filters | 없음 | **Middleware 파이프라인** |
| 현재 상태 | GA | 유지보수 모드 | **Public Preview** |

### 아키텍처 개요

```
[사용자 입력]
     │
     ▼
[Middleware Pipeline]   ← 로깅, 인증, 레이트 리밋, PII 마스킹 등
     │
     ▼
[AIAgent]
  ├─ instructions (시스템 프롬프트)
  ├─ tools        (Function, MCP, 다른 Agent)
  └─ LLM Client   (Azure OpenAI Chat Completions)
          │
          ▼
    [Tool 호출 루프]    ← ReAct 패턴 (Reasoning + Acting)
          │
          ▼
    [AgentSession]     ← 대화 히스토리 유지 (멀티턴)
          │
          ▼
    [응답 반환]
```

### 환경 준비

**.NET 10 SDK** 설치 후 다음 패키지를 추가합니다.

```bash
dotnet new console -n AgentSamples -f net10.0
cd AgentSamples

# 핵심 패키지
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity

# RAG용 (5장)
dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease

# 관측성 (7장)
dotnet add package OpenTelemetry.Exporter.Console
```

> `Microsoft.Agents.AI.OpenAI`는 `Microsoft.Agents.AI`와 `Microsoft.Extensions.AI`를 의존성으로 포함하므로 별도 설치가 불필요합니다.

**Azure OpenAI 설정** — `appsettings.local.json` 또는 환경 변수로 주입합니다.

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

> **보안 팁**: API 키를 코드에 직접 쓰지 마세요. `AzureKeyCredential`은 환경 변수나 Key Vault에서 읽어오고, 로컬 개발에는 `AzureCliCredential`(`az login`)을 활용합니다.

---

## 2장. 첫 번째 Agent 만들기

### AIAgent와 AsAIAgent()

`AIAgent`는 LLM 클라이언트를 감싸는 래퍼입니다. 단순 API 호출에서 벗어나 시스템 프롬프트 관리, Tool 호출 루프, 대화 히스토리, 미들웨어 파이프라인을 자동으로 처리합니다.

Agent Framework의 핵심 진입점은 **`AsAIAgent()` 확장 메서드**입니다.

```csharp
using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;

var endpoint    = config["AzureOpenAI:Endpoint"] ?? config["AZURE_OPENAI_ENDPOINT"]!;
var apiKey      = config["AzureOpenAI:ApiKey"]   ?? config["AZURE_OPENAI_API_KEY"]!;
var deployment  = config["AzureOpenAI:DeploymentName"] ?? "gpt-4o-mini";

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deployment)
    .AsAIAgent(
        instructions: "당신은 친절한 AI 어시스턴트입니다. 한국어로 대답하세요.",
        name: "HelloAgent"
    );
```

이 3줄 체인이 Agent Framework의 기본 패턴입니다.

1. `AzureOpenAIClient` — Azure OpenAI 연결
2. `GetChatClient()` — Chat Completions 엔드포인트 선택
3. `AsAIAgent()` — Agent 래퍼 생성

### 단일 응답: RunAsync()

```csharp
AgentResponse response = await agent.RunAsync("안녕하세요! 자기소개를 해주세요.");
Console.WriteLine(response.Text);
```

전체 응답이 완성될 때까지 기다렸다가 한 번에 반환합니다. API 응답이나 배치 처리에 적합합니다.

### 스트리밍 응답: RunStreamingAsync()

```csharp
Console.Write("Agent: ");
await foreach (var update in agent.RunStreamingAsync("대한민국의 수도는 어디인가요?"))
{
    Console.Write(update.Text);  // 토큰 단위로 즉시 출력
}
Console.WriteLine();
```

LLM이 토큰을 생성하는 즉시 스트리밍합니다. 챗봇 UI처럼 "타이핑되는" 효과를 원할 때 사용합니다.

| 항목 | `RunAsync()` | `RunStreamingAsync()` |
|---|---|---|
| 반환 타입 | `Task<AgentResponse>` | `IAsyncEnumerable<AgentResponse>` |
| 응답 시점 | 전체 완성 후 | 토큰 생성 즉시 |
| 적합한 경우 | API 응답, 배치 처리 | 챗봇 UI, 터미널 |

### 에러 처리

```csharp
try
{
    var response = await agent.RunAsync("질문입니다.");
    Console.WriteLine(response.Text);
}
catch (RequestFailedException ex) when (ex.Status == 401)
{
    Console.Error.WriteLine($"인증 오류: API 키 또는 az login을 확인하세요. ({ex.Message})");
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    Console.Error.WriteLine($"배포를 찾을 수 없습니다: {deployment} ({ex.Message})");
}
catch (RequestFailedException ex)
{
    Console.Error.WriteLine($"Azure API 오류 [{ex.Status}]: {ex.Message}");
}
```

```bash
dotnet run -- 01
```

---

## 3장. Function Calling — Tool로 Agent 능력 확장

### Function Calling이란?

LLM 단독으로는 실시간 데이터(날씨, 주가, 시각)를 알 수 없고, 외부 시스템과 상호작용할 수도 없습니다. Function Calling은 이 한계를 해결하는 메커니즘입니다.

```
사용자 메시지 ──▶ LLM 추론 ──▶ Tool 선택 & 인수 생성
                                      │
                              C# 함수 실제 실행
                                      │
                    Tool 결과 ──▶ LLM 최종 응답 생성
```

프레임워크가 이 전체 루프를 자동으로 처리합니다. 개발자는 C# 함수 작성에만 집중하면 됩니다.

### AIFunctionFactory.Create()

`Microsoft.Extensions.AI` 패키지가 제공하는 팩토리 메서드로, 람다 또는 메서드를 `AITool`로 변환합니다.

```csharp
using Microsoft.Extensions.AI;
using System.ComponentModel;

AITool weatherTool = AIFunctionFactory.Create(
    ([Description("날씨를 조회할 도시 이름")] string city) =>
    {
        var data = new Dictionary<string, string>
        {
            ["서울"] = "맑음, 22°C",
            ["부산"] = "흐림, 19°C",
        };
        return data.TryGetValue(city, out var w) ? $"{city}: {w}" : "정보 없음";
    },
    name: "GetCurrentWeather",
    description: "지정한 도시의 현재 날씨를 반환합니다."
);
```

**`[Description]` 어트리뷰트의 역할**: LLM은 함수의 실제 구현을 볼 수 없습니다. 파라미터 이름과 `[Description]`만으로 언제, 어떻게 도구를 호출할지 판단합니다. 설명을 구체적으로 작성할수록 LLM의 Tool 선택 정확도가 올라갑니다.

### 3개 Tool 등록 예제

```csharp
// Tool 1: 날씨 조회 (위에서 정의)

// Tool 2: 현재 시각
AITool timeTool = AIFunctionFactory.Create(
    () => $"현재 시각: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
    name: "GetCurrentTime",
    description: "현재 날짜와 시각을 반환합니다."
);

// Tool 3: 사칙연산
AITool calculatorTool = AIFunctionFactory.Create(
    (
        [Description("첫 번째 피연산자")] double a,
        [Description("연산자: +, -, *, /")] string op,
        [Description("두 번째 피연산자")] double b
    ) => op switch
    {
        "+" => $"{a} + {b} = {a + b}",
        "-" => $"{a} - {b} = {a - b}",
        "*" => $"{a} × {b} = {a * b}",
        "/" => b == 0 ? "0으로 나눌 수 없음" : $"{a} ÷ {b} = {a / b}",
        _   => $"알 수 없는 연산자: {op}"
    },
    name: "Calculate",
    description: "두 수의 사칙연산을 수행합니다."
);

// Agent에 등록
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deployment)
    .AsAIAgent(
        instructions: "도구가 필요하면 반드시 사용하고, 항상 한국어로 답하세요.",
        name: "ToolAgent",
        tools: [weatherTool, timeTool, calculatorTool]
    );

// 실행 — Tool 호출/결과 처리는 프레임워크가 자동 수행
AgentResponse response = await agent.RunAsync("서울 날씨와 현재 시각을 알려주세요.");
Console.WriteLine(response.Text);
```

### 멀티 Tool 호출

하나의 질문에 여러 Tool이 필요하면 LLM이 자동으로 필요한 Tool들을 순서대로 (또는 병렬로) 호출합니다.

```csharp
// "날씨 + 계산" — 두 Tool을 한 번의 RunAsync()로 처리
var response = await agent.RunAsync(
    "제주도 날씨를 확인하고, 123 곱하기 456도 계산해줘."
);
// 예상 출력: 제주도는 비, 17°C입니다. 123 × 456 = 56,088입니다.
```

### Tool 보안 고려사항

| 위험 | 대처 방법 |
|------|----------|
| Tool이 민감한 데이터에 접근 | 필요한 데이터만 반환, 내부 구현 숨기기 |
| LLM이 의도치 않은 Tool 호출 | `description`을 명확히 작성해 호출 범위 제한 |
| 외부 API 오류 | Tool 내부에서 예외 처리 후 오류 메시지 문자열 반환 |

```bash
dotnet run -- 02
```

---

## 4장. 메모리와 세션 관리

### Agent의 기억 문제

기본 `RunAsync()`는 **상태 비저장(stateless)** 방식입니다. 매 호출이 독립적이어서 이전 대화를 기억하지 못합니다.

```csharp
// ❌ 이전 대화를 기억하지 못하는 예
await agent.RunAsync("제 이름은 홍길동입니다.");
var r = await agent.RunAsync("제 이름이 뭐라고 했죠?");
// → "죄송합니다, 이전 대화 내용을 알 수 없습니다."
```

멀티턴 대화를 구현하려면 대화 히스토리를 직접 관리해야 합니다.

### ChatHistory를 직접 전달하는 방식

```csharp
using OpenAI.Chat;

var history = new List<ChatMessage>();

// 첫 번째 메시지
history.Add(new UserChatMessage("제 이름은 홍길동입니다."));
AgentResponse r1 = await agent.RunAsync(history);
history.Add(new AssistantChatMessage(r1.Text));

// 두 번째 메시지 — 이전 대화를 기억함
history.Add(new UserChatMessage("제 이름이 뭐라고 했죠?"));
AgentResponse r2 = await agent.RunAsync(history);
Console.WriteLine(r2.Text);
// → "홍길동이라고 하셨습니다."
```

### 히스토리 관리 유틸리티

실제 프로젝트에서는 히스토리를 래핑하는 클래스를 만드는 것이 일반적입니다.

```csharp
public class ConversationSession
{
    private readonly List<ChatMessage> _history = new();
    private readonly int _maxTurns;

    public ConversationSession(int maxTurns = 20) => _maxTurns = maxTurns;

    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

    public void AddUserMessage(string message)
        => _history.Add(new UserChatMessage(message));

    public void AddAssistantMessage(string message)
        => _history.Add(new AssistantChatMessage(message));

    /// <summary>오래된 메시지를 제거해 토큰 한도 초과를 방지합니다.</summary>
    public void TrimIfNeeded()
    {
        while (_history.Count > _maxTurns * 2)
            _history.RemoveAt(0);
    }
}
```

**대화 루프 예제:**

```csharp
var session = new ConversationSession(maxTurns: 10);

while (true)
{
    Console.Write("사용자: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;

    session.AddUserMessage(input);
    session.TrimIfNeeded();

    AgentResponse response = await agent.RunAsync(
        new List<ChatMessage>(session.History));

    session.AddAssistantMessage(response.Text);
    Console.WriteLine($"Agent: {response.Text}");
}
```

### 히스토리 요약(Summarization) 전략

대화가 길어지면 토큰 비용이 급증합니다. 요약 Agent를 별도로 두어 오래된 대화를 압축하는 전략이 효과적입니다.

```
[전체 히스토리]
  메시지 1~10  ──▶  요약 Agent  ──▶  "사용자는 홍길동, 서울 거주..."
  메시지 11~20 ──▶  현재 컨텍스트에 포함
                                        │
                              [요약 + 최근 메시지]만 Main Agent에 전달
```

```csharp
// 요약 전용 Agent
AIAgent summaryAgent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deployment)
    .AsAIAgent(
        instructions: "대화 내용을 200자 이내로 핵심만 요약하세요.",
        name: "SummaryAgent"
    );

// 오래된 부분 요약
var oldMessages = history.Take(history.Count - 5).ToList();
AgentResponse summary = await summaryAgent.RunAsync(oldMessages);

// 요약 + 최근 5개 메시지로 새 히스토리 구성
var trimmedHistory = new List<ChatMessage>
{
    new SystemChatMessage($"이전 대화 요약: {summary.Text}")
};
trimmedHistory.AddRange(history.TakeLast(5));
```

### 세션 영속성

인메모리 히스토리는 프로세스 재시작 시 소멸됩니다. 영속성이 필요하면 직렬화 후 외부 저장소에 저장합니다.

```csharp
var historyDto = history.Select(m => new
{
    Role    = m.Role.ToString(),
    Content = m.Content.FirstOrDefault()?.Text ?? ""
}).ToList();

var json = JsonSerializer.Serialize(historyDto);
await File.WriteAllTextAsync("session.json", json);
// 또는 Redis, CosmosDB, SQL 등 외부 저장소 활용
```

| 항목 | 내용 |
|------|------|
| 멀티턴 기본 방식 | `IList<ChatMessage>` 히스토리를 `RunAsync()`에 전달 |
| 히스토리 관리 | `UserChatMessage` / `AssistantChatMessage` 누적 |
| 토큰 절약 | 최대 턴 수 제한 + 오래된 메시지 제거 또는 요약 |
| 영속성 | 직렬화 후 외부 저장소(파일/DB/Redis) 활용 |

```bash
dotnet run -- 03
```

---

## 마무리

1편에서는 Microsoft Agent Framework의 기초를 다졌습니다.

- **1장**: 프레임워크 개요, 환경 설정, 패키지 설치
- **2장**: `AIAgent` 생성, `RunAsync()` / `RunStreamingAsync()` 사용법
- **3장**: `AIFunctionFactory`로 Tool 만들기, 멀티 Tool 호출
- **4장**: `ChatHistory` 기반 멀티턴, 히스토리 요약, 세션 영속성

**2편**에서는 외부 문서를 검색해 답변하는 **RAG**, 여러 에이전트를 협업시키는 **멀티 에이전트**, 그리고 로깅·PII 마스킹·OpenTelemetry를 다루는 **미들웨어 & 관측성**을 다룹니다.

전체 샘플 코드는 `samples/` 디렉터리를 참고하세요.
