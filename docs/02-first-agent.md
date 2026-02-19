# 2장: 첫 번째 Agent 만들기

> **버전 정보**: `Microsoft.Agents.AI.OpenAI` `1.0.0-preview.260212.1` 기준
> 샘플 코드: [`samples/01_HelloAgent.cs`](../samples/01_HelloAgent.cs)

---

## 2.1 AIAgent란?

`AIAgent`는 LLM(Large Language Model) 클라이언트를 감싸는 래퍼(wrapper) 클래스입니다. 단순한 API 호출을 넘어 다음 기능을 자동으로 처리합니다:

- **시스템 프롬프트 관리**: `instructions` 파라미터로 Agent 역할 정의
- **Tool 호출 루프**: LLM이 Tool 호출을 요청하면 자동으로 실행 후 결과를 다시 LLM에 전달
- **대화 히스토리**: `AgentSession`을 통해 멀티턴 대화 컨텍스트 유지
- **미들웨어 파이프라인**: 요청/응답을 가로채는 Middleware 체인

### AsAIAgent() 확장 메서드

Agent Framework의 핵심 진입점은 `AsAIAgent()` 확장 메서드입니다. Azure OpenAI의 Chat 클라이언트에 이 메서드를 호출하면 즉시 Agent로 변환됩니다:

```csharp
AIAgent agent = new AzureOpenAIClient(endpoint, credential)
    .GetChatClient("gpt-4o-mini")   // Chat Completions 클라이언트
    .AsAIAgent(                      // Agent로 변환
        instructions: "당신은 친절한 AI 어시스턴트입니다.",
        name: "MyAgent"
    );
```

이 3줄 체인이 Agent Framework의 기본 패턴입니다:
1. `AzureOpenAIClient` — Azure OpenAI 연결
2. `GetChatClient()` — Chat Completions 엔드포인트 선택
3. `AsAIAgent()` — Agent 래퍼 생성 (`instructions`, `name`, `tools`, `middleware` 등 설정)

---

## 2.2 기본 Agent 예제

전체 코드: [`samples/01_HelloAgent.cs`](../samples/01_HelloAgent.cs)

### 환경 변수 로드 및 클라이언트 초기화

```csharp
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;

// 환경 변수에서 설정값 로드
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT 환경 변수를 설정하세요.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-4o-mini";

// AzureCliCredential: az login 인증 사용 (API 키 불필요)
AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions: "당신은 친절한 AI 어시스턴트입니다. 한국어로 대답하세요.",
        name: "HelloAgent"
    );
```

---

## 2.3 단일 응답: RunAsync()

`RunAsync()`는 전체 응답이 완성될 때까지 기다렸다가 결과를 한 번에 반환합니다.

```csharp
string response = await agent.RunAsync("안녕하세요! 자기소개를 해주세요.");
Console.WriteLine(response);
```

**반환 타입**: `Task<string>`

**언제 사용하는가?**
- API 응답으로 전체 텍스트를 한 번에 반환해야 할 때
- 후속 처리(파싱, 변환 등)가 필요한 경우
- 응답 시간이 짧은 짧은 질의응답

---

## 2.4 스트리밍 응답: RunStreamingAsync()

`RunStreamingAsync()`는 LLM이 토큰을 생성하는 즉시 `IAsyncEnumerable<string>`으로 스트리밍합니다. 사용자가 응답이 "타이핑되는" 느낌을 받아 UX가 좋아집니다.

```csharp
Console.Write("Agent: ");
await foreach (string chunk in agent.RunStreamingAsync("대한민국의 수도는 어디인가요?"))
{
    Console.Write(chunk);  // 토큰 단위로 즉시 출력
}
Console.WriteLine();
```

**반환 타입**: `IAsyncEnumerable<string>`

**언제 사용하는가?**
- 챗봇 UI처럼 타이핑 효과를 보여줄 때
- 응답이 길어서 사용자가 기다리는 느낌을 줄이고 싶을 때
- 실시간 대시보드나 터미널 출력

### RunAsync() vs RunStreamingAsync() 비교

| 항목 | `RunAsync()` | `RunStreamingAsync()` |
|---|---|---|
| 반환 타입 | `Task<string>` | `IAsyncEnumerable<string>` |
| 응답 시점 | 전체 완성 후 | 토큰 생성 즉시 |
| 코드 복잡도 | 낮음 | 약간 높음 (`await foreach`) |
| UX | 지연 후 한 번에 표시 | 실시간 타이핑 효과 |
| 적합한 경우 | API 응답, 배치 처리 | 챗봇 UI, 터미널 |

---

## 2.5 에러 처리 패턴

Agent를 실제 서비스에서 사용할 때는 아래 세 가지 오류 유형을 처리해야 합니다.

```csharp
using Azure;

try
{
    string response = await agent.RunAsync("질문입니다.");
    Console.WriteLine(response);
}
catch (RequestFailedException ex) when (ex.Status == 401)
{
    // 인증 오류: az login이 안 되어 있거나 토큰 만료
    Console.Error.WriteLine($"인증 오류: az login을 실행하세요. ({ex.Message})");
}
catch (RequestFailedException ex) when (ex.Status == 404)
{
    // 배포 이름 오류: AZURE_OPENAI_DEPLOYMENT_NAME이 잘못됨
    Console.Error.WriteLine($"배포를 찾을 수 없습니다: {deploymentName} ({ex.Message})");
}
catch (RequestFailedException ex)
{
    // 그 외 Azure API 오류 (레이트 리밋, 서비스 불가 등)
    Console.Error.WriteLine($"Azure API 오류 [{ex.Status}]: {ex.Message}");
}
catch (Exception ex)
{
    // 예상치 못한 오류
    Console.Error.WriteLine($"오류: {ex.Message}");
}
```

**흔한 오류 3가지:**

| 오류 | 원인 | 해결 방법 |
|---|---|---|
| `401 Unauthorized` | `az login` 미실행 또는 토큰 만료 | `az login` 재실행 |
| `404 Not Found` | 배포 이름 오타 또는 리소스 미배포 | Azure Portal에서 배포 이름 확인 |
| `InvalidOperationException` | 환경 변수 누락 | `.env` 또는 `user-secrets` 확인 |

---

## 2.6 샘플 코드 실행 방법

```bash
# 1. 환경 변수 설정 (미설정 시)
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"

# 2. 샘플 실행
cd samples
dotnet run --project AgentSamples.csproj -- 01
```

예상 출력:
```
=== 01: Hello Agent ===

[단일 응답]
저는 Azure OpenAI를 기반으로 작동하는 AI 어시스턴트입니다...

[스트리밍 응답]
Agent: 대한민국의 수도는 서울입니다...
```

---

## 다음 단계

기본 Agent 실행에 성공했다면, 다음 챕터에서 Agent가 외부 함수를 호출하는 **Function Calling** 을 배워봅시다.

[← 1장: 소개 및 환경 설정](01-introduction.md) | [3장: Function Calling / Tool Use →](03-function-calling.md)
