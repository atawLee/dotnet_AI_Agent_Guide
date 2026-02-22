# 6장. 멀티 에이전트 협업

> **주의**: Microsoft Agent Framework는 현재 **public preview** (`1.0.0-preview.260212.1`) 상태입니다. API는 정식 출시 전에 변경될 수 있습니다.

## 멀티 에이전트란?

단일 에이전트는 모든 역할을 혼자 처리합니다.  
**멀티 에이전트**는 역할을 나눠 여러 에이전트가 협업하여 복잡한 문제를 처리합니다.

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

## 이 샘플의 구현 패턴

Microsoft Agent Framework에서 멀티 에이전트를 구현하는 가장 자연스러운 방법은  
**전문 에이전트를 Tool로 래핑**하여 오케스트레이터에게 등록하는 것입니다.

오케스트레이터는 LLM의 Function Calling 능력으로 적절한 전문 에이전트를 자동 선택·호출합니다.

## 전문 에이전트 생성

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

## 전문 에이전트를 Tool로 래핑

`AIFunctionFactory.Create()`로 에이전트 호출 로직을 Tool로 만듭니다.

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

`description`은 LLM이 어떤 Tool을 선택할지 판단하는 기준이 됩니다. **명확하고 구체적으로** 작성하세요.

## 오케스트레이터 에이전트

전문 에이전트 Tool들을 장착하고 라우팅을 담당합니다.

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
        tools: [weatherTool, calculatorTool]   // 전문 에이전트 Tool 등록
    );
```

## 호출

사용자는 오케스트레이터 하나만 알면 됩니다. 라우팅은 내부에서 자동으로 이루어집니다.

```csharp
await foreach (var update in orchestrator.RunStreamingAsync(question, session: null))
    Console.Write(update.Text);
```

**실행 흐름 예시:**

```
사용자: "오늘 서울 날씨 알려줘"
  → 오케스트레이터 LLM: ask_weather_agent("오늘 서울 날씨 알려줘") 호출 결정
  → WeatherAgent 실행 → 날씨 응답 반환
  → 오케스트레이터가 최종 답변 생성

사용자: "부산 내일 비 올 확률이랑 27 더하기 38은?"
  → 오케스트레이터 LLM: ask_weather_agent + ask_calculator_agent 동시 호출 결정
  → 두 에이전트 병렬 실행 → 결과 취합
  → 오케스트레이터가 통합 답변 생성
```

## 실행

```bash
dotnet run -- 06
```

예상 출력:
```
=== 06: 멀티 에이전트 협업 ===

사용자: 오늘 서울 날씨가 어때?
  [WeatherAgent 호출] 질문: 오늘 서울 날씨가 어때?
최종 답변: 오늘 서울은 맑고 기온은 22°C입니다.

사용자: 123 곱하기 456은 얼마야?
  [CalculatorAgent 호출] 식: 123 곱하기 456은 얼마야?
최종 답변: 123 × 456 = 56,088입니다.
```

## 전체 코드

`samples/05_MultiAgent.cs` 참조.

## 멀티 에이전트 설계 팁

| 고려 사항 | 설명 |
|-----------|------|
| **에이전트 경계** | 각 에이전트의 역할을 명확히 분리. 겹치는 영역이 많으면 오케스트레이터가 혼란 |
| **Tool description** | LLM이 라우팅 판단에 사용하므로 구체적으로 작성 |
| **에러 처리** | 전문 에이전트 실패 시 오케스트레이터가 대체 응답을 제공할 수 있도록 |
| **컨텍스트 공유** | 세션을 에이전트 간에 공유할지, 독립적으로 유지할지 설계 필요 |
| **비용** | 호출마다 LLM API 비용 발생. 간단한 라우팅은 규칙 기반으로 처리도 고려 |

## 다음 단계

- [7장. 미들웨어 & 관측성](./07-middleware-observability.md) — 로깅, 트레이싱, OpenTelemetry
