# Chapter 3: Function Calling — Tool로 Agent 능력 확장

> **⚠️ 주의**: Microsoft Agent Framework는 현재 **public preview** 상태입니다 (`1.0.0-preview.*`).  
> API 및 동작이 정식 출시 전에 변경될 수 있습니다.

---

## 이 챕터에서 배울 것

- **Function Calling**(도구 호출)의 개념과 동작 원리
- `AIFunctionFactory.Create()`로 C# 함수를 AITool로 변환하는 방법
- `[Description]` 어트리뷰트로 LLM에게 도구 설명을 제공하는 방법
- 여러 Tool을 동시에 호출하는 멀티 Tool 호출 패턴

샘플 코드: [`samples/02_AddTools.cs`](../samples/02_AddTools.cs)

---

## 1. Function Calling이란?

LLM 단독으로는 **실시간 데이터**(날씨, 주가, 시각)를 알 수 없고, **외부 시스템**(데이터베이스, API)과 상호작용할 수도 없습니다. Function Calling은 이 한계를 해결하는 메커니즘입니다.

```
사용자 메시지 ──▶ LLM 추론 ──▶ Tool 선택 & 인수 생성
                                      │
                              C# 함수 실제 실행
                                      │
                    Tool 결과 ──▶ LLM 최종 응답 생성
```

프레임워크가 이 전체 루프를 자동으로 처리하므로, 개발자는 **C# 함수 작성**에만 집중하면 됩니다.

---

## 2. AIFunctionFactory.Create()

`Microsoft.Extensions.AI` 패키지가 제공하는 팩토리 메서드로, 람다 또는 메서드를 `AITool`로 변환합니다.

```csharp
using Microsoft.Extensions.AI;
using System.ComponentModel;

AITool myTool = AIFunctionFactory.Create(
    ([Description("도시 이름")] string city) =>
    {
        return $"{city}의 날씨: 맑음 22°C";
    },
    name: "GetWeather",              // LLM이 호출할 때 사용하는 이름
    description: "도시의 날씨를 조회합니다."  // LLM이 도구를 선택할 때 참조
);
```

### [Description] 어트리뷰트의 역할

LLM은 함수의 실제 구현을 볼 수 없습니다. **파라미터 이름**과 **`[Description]` 어트리뷰트**만으로 언제, 어떻게 도구를 호출해야 하는지 판단합니다.

```csharp
// 나쁜 예 — 파라미터 설명 없음
AITool tool = AIFunctionFactory.Create((string a, string b) => ...);

// 좋은 예 — 명확한 설명 제공
AITool tool = AIFunctionFactory.Create((
    [Description("계산할 첫 번째 숫자")] double a,
    [Description("연산자: +, -, *, /")] string op,
    [Description("계산할 두 번째 숫자")] double b
) => ...);
```

---

## 3. Tool 등록

`AsAIAgent()`의 세 번째 위치 인수 `tools`에 `IList<AITool>`을 전달합니다.

```csharp
using OpenAI.Chat;

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "날씨, 시간, 계산이 필요하면 도구를 사용하세요.",
        name: "ToolAgent",
        tools: [weatherTool, timeTool, calculatorTool]   // ← Tool 목록
    );
```

> **참고**: `tools` 파라미터는 `IList<AITool>` 타입입니다. C# 컬렉션 식(`[...]`) 또는 `new List<AITool> { ... }` 모두 사용 가능합니다.

---

## 4. 전체 예제: 3개 Tool 등록

```csharp
// Tool 1: 날씨 조회
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
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "도구가 필요하면 반드시 사용하고, 항상 한국어로 답하세요.",
        name: "ToolAgent",
        tools: [weatherTool, timeTool, calculatorTool]
    );

// 실행 — Tool 호출/결과 처리는 프레임워크가 자동 수행
AgentResponse response = await agent.RunAsync("서울 날씨를 알려주세요.");
Console.WriteLine(response.Text);
```

---

## 5. 멀티 Tool 호출

하나의 질문에 여러 Tool이 필요한 경우, LLM이 자동으로 필요한 Tool들을 순서대로 (또는 병렬로) 호출합니다.

```csharp
// "제주도 날씨 + 현재 시각" — 두 Tool을 한 번의 RunAsync()로 처리
AgentResponse response = await agent.RunAsync(
    "제주도 날씨를 확인하고, 현재 시각도 알려주세요."
);
Console.WriteLine(response.Text);
// 예상 출력:
// 제주도의 현재 날씨는 비, 17°C입니다. 현재 시각은 2025-01-15 14:30:00입니다.
```

---

## 6. 비동기 Tool

Tool 함수도 `async`로 만들 수 있습니다. 실제 외부 API를 호출할 때 유용합니다.

```csharp
AITool asyncWeatherTool = AIFunctionFactory.Create(
    async ([Description("도시 이름")] string city) =>
    {
        // 실제 외부 API 호출 예시 (HttpClient 사용)
        using var http = new HttpClient();
        var result = await http.GetStringAsync(
            $"https://api.example.com/weather?city={city}");
        return result;
    },
    name: "GetWeatherAsync",
    description: "외부 API에서 날씨 정보를 조회합니다."
);
```

---

## 7. Tool 보안 고려사항

| 위험 | 대처 방법 |
|------|----------|
| Tool이 민감한 데이터에 접근 | 필요한 데이터만 반환, 내부 구현 숨기기 |
| LLM이 의도치 않은 Tool 호출 | `description`을 명확히 작성해 호출 범위 제한 |
| 외부 API 오류 | Tool 내부에서 예외 처리 후 오류 메시지 문자열 반환 |
| 비용 / 레이트 리밋 | Tool 내부에서 캐싱 또는 호출 횟수 제한 구현 |

---

## 8. 핵심 정리

| 항목 | 내용 |
|------|------|
| Tool 생성 | `AIFunctionFactory.Create(람다, name, description)` |
| 파라미터 설명 | `[Description("설명")]` 어트리뷰트 |
| Tool 등록 | `AsAIAgent(..., tools: [tool1, tool2, ...])` |
| Tool 실행 | 프레임워크가 자동 처리 (개발자 개입 불필요) |
| 비동기 지원 | Tool 람다를 `async`로 선언 가능 |

---

## 다음 챕터

[Chapter 4: 메모리와 세션 관리 →](04-memory-session.md)
