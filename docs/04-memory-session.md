# Chapter 4: 메모리와 세션 관리

> **⚠️ 주의**: Microsoft Agent Framework는 현재 **public preview** 상태입니다 (`1.0.0-preview.*`).  
> API 및 동작이 정식 출시 전에 변경될 수 있습니다.

---

## 이 챕터에서 배울 것

- **대화 컨텍스트(Multi-Turn)** 유지 방법
- **스레드(Thread)** 기반 세션 관리
- 인메모리 히스토리와 외부 저장소 연동 패턴
- 대화 히스토리 요약(Summarization) 전략

샘플 코드:
- [`samples/03_MultiTurn.cs`](../samples/03_MultiTurn.cs) — 기본 멀티턴
- [`samples/04_Memory.cs`](../samples/04_Memory.cs) — 세션 메모리

---

## 1. Agent의 기억 문제

기본 `RunAsync()`는 **상태 비저장(stateless)** 방식입니다. 매 호출이 독립적이어서 이전 대화를 기억하지 못합니다.

```csharp
// ❌ 이전 대화를 기억하지 못하는 예
await agent.RunAsync("제 이름은 홍길동입니다.");
AgentResponse r = await agent.RunAsync("제 이름이 뭐라고 했죠?");
// → "죄송합니다, 이전 대화 내용을 알 수 없습니다."
```

멀티턴 대화를 구현하려면 **대화 히스토리를 직접 관리**하거나, **Thread/Session** 객체를 사용해야 합니다.

---

## 2. ChatHistory를 직접 전달하는 방식

가장 간단한 방법은 `RunAsync()`에 `IList<ChatMessage>` 형태의 히스토리를 누적해 전달하는 것입니다.

```csharp
using OpenAI.Chat;

// 대화 히스토리를 직접 관리
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

### 히스토리 직접 관리의 장단점

| 장점 | 단점 |
|------|------|
| 구현이 단순 | 히스토리가 길어지면 토큰 비용 증가 |
| 완전한 제어 가능 | 컨텍스트 길이 제한 직접 관리 필요 |
| 외부 의존성 없음 | 세션 간 영속성 직접 구현 필요 |

---

## 3. 히스토리 관리 유틸리티 클래스

실제 프로젝트에서는 히스토리를 래핑하는 유틸리티 클래스를 만드는 것이 일반적입니다.

```csharp
public class ConversationSession
{
    private readonly List<ChatMessage> _history = new();
    private readonly int _maxTurns;

    public ConversationSession(int maxTurns = 20)
    {
        _maxTurns = maxTurns;
    }

    public IReadOnlyList<ChatMessage> History => _history.AsReadOnly();

    public void AddUserMessage(string message)
        => _history.Add(new UserChatMessage(message));

    public void AddAssistantMessage(string message)
        => _history.Add(new AssistantChatMessage(message));

    /// <summary>오래된 메시지를 제거해 토큰 한도 초과를 방지합니다.</summary>
    public void TrimIfNeeded()
    {
        // 최대 턴 수 초과 시 가장 오래된 교환(사용자+어시스턴트 쌍)을 제거
        while (_history.Count > _maxTurns * 2)
        {
            _history.RemoveAt(0); // 가장 오래된 메시지 제거
        }
    }
}
```

---

## 4. 대화 흐름 예제

```csharp
var session = new ConversationSession(maxTurns: 10);

while (true)
{
    Console.Write("사용자: ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) break;

    session.AddUserMessage(input);
    session.TrimIfNeeded();

    // 전체 히스토리를 포함해 Agent 호출
    AgentResponse response = await agent.RunAsync(
        new List<ChatMessage>(session.History));

    session.AddAssistantMessage(response.Text);
    Console.WriteLine($"Agent: {response.Text}");
}
```

---

## 5. 히스토리 요약(Summarization) 전략

대화가 길어지면 토큰 비용이 급증합니다. **요약 Agent**를 별도로 두어 오래된 대화를 압축하는 전략을 사용할 수 있습니다.

```
[전체 히스토리]
  메시지 1~10  ──▶  요약 Agent  ──▶  "사용자는 홍길동, 서울 거주..."
  메시지 11~20 ──▶  현재 컨텍스트에 포함
                                        │
                              [요약 + 최근 메시지]만 Main Agent에 전달
```

```csharp
// 요약 전용 Agent
AIAgent summaryAgent = chatClient.AsAIAgent(
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

---

## 6. 세션 영속성

인메모리 히스토리는 프로세스 재시작 시 소멸됩니다. 영속성이 필요하다면 히스토리를 직렬화해 외부 저장소에 저장합니다.

```csharp
// JSON 직렬화 예시 (실제 구현은 프로젝트 요구사항에 맞게 조정)
var historyDto = history.Select(m => new
{
    Role = m.Role.ToString(),
    Content = m.Content.FirstOrDefault()?.Text ?? ""
}).ToList();

var json = JsonSerializer.Serialize(historyDto);
await File.WriteAllTextAsync("session.json", json);   // 파일 저장
// 또는 Redis, CosmosDB, SQL 등 외부 저장소에 저장
```

---

## 7. 핵심 정리

| 항목 | 내용 |
|------|------|
| 멀티턴 기본 방식 | `IList<ChatMessage>` 히스토리를 `RunAsync()`에 전달 |
| 히스토리 관리 | `List<ChatMessage>`에 `UserChatMessage` / `AssistantChatMessage` 누적 |
| 토큰 절약 | 최대 턴 수 제한 + 오래된 메시지 제거 또는 요약 |
| 영속성 | 직렬화 후 외부 저장소(파일/DB/Redis) 활용 |

---

## 다음 챕터

[Chapter 5: RAG — 검색 증강 생성 →](05-rag.md)
