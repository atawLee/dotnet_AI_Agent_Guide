# .NET 10 / C# AI Agent 가이드 작업 계획

> 작성일: 2026-02-19  
> 상태: 계획 수립 완료, 작성 대기 중

---

## 1. 프로젝트 개요

### 목적
Microsoft Agent Framework(`microsoft/agent-framework`)를 사용하여 .NET 10 / C#으로 AI Agent를 구축하는 실용적인 한국어 가이드를 작성한다.

### 대상 독자
- C# 중급 개발자
- .NET 기초 지식 보유, AI Agent 개념은 처음인 개발자
- Python AI 생태계(LangChain, AutoGen 등)를 경험하고 .NET으로 전환을 검토하는 개발자

### 핵심 기술 스택
| 항목 | 선택 | 비고 |
|---|---|---|
| 언어 / 런타임 | C# 13 / .NET 10 | 2025년 11월 GA |
| AI Agent 프레임워크 | **Microsoft Agent Framework** | `Microsoft.Agents.AI` NuGet |
| LLM 공급자 | **Azure OpenAI** | Chat Completions / Responses API |
| Vector Store (RAG) | **InMemory Vector Store** | `Microsoft.Extensions.VectorData` |
| 인증 | Azure CLI Credential (`az login`) | 로컬 개발 편의성 |
| 관측성 | OpenTelemetry + Console Exporter | `Microsoft.Extensions.AI` 내장 |

> **주의**: Microsoft Agent Framework는 현재 **public preview** 상태입니다 (최신 버전: `1.0.0-preview.260212.1`). API가 GA 전에 변경될 수 있으므로 가이드 전체에 이를 명시합니다.

---

## 2. 가이드 구조 및 파일 목록

### 디렉토리 레이아웃

```
blog_ai_agent_dotnet/
├── Plan.md                              # 이 파일 - 작업 계획
├── README.md                            # 전체 가이드 인덱스 & 시작 안내
│
├── docs/                                # 챕터별 Markdown 문서
│   ├── 01-introduction.md               # 1장: 소개 및 환경 설정
│   ├── 02-first-agent.md                # 2장: 첫 번째 Agent 만들기
│   ├── 03-function-calling.md           # 3장: Function Calling / Tool Use
│   ├── 04-memory-session.md             # 4장: 메모리 & 세션 관리
│   ├── 05-rag.md                        # 5장: RAG (검색 증강 생성)
│   ├── 06-multi-agent.md                # 6장: 멀티 에이전트 패턴
│   └── 07-middleware-observability.md   # 7장: 미들웨어 & 관측성
│
└── samples/                             # 챕터별 실행 가능한 C# 코드
    ├── AgentSamples.csproj              # 프로젝트 파일
    ├── 01_HelloAgent.cs                 # 2장 예제
    ├── 02_AddTools.cs                   # 3장 예제
    ├── 03_MultiTurn.cs                  # 4장 예제 (세션)
    ├── 04_Memory.cs                     # 4장 예제 (Context Provider)
    ├── 05_RAG.cs                        # 5장 예제
    ├── 06_MultiAgent.cs                 # 6장 예제
    └── 07_Middleware.cs                 # 7장 예제
```

---

## 3. 챕터별 상세 작업 명세

---

### Chapter 1: 소개 및 환경 설정 (`docs/01-introduction.md`)

#### 작성 목표
독자가 "왜 이 프레임워크를 써야 하는가"와 "개발 환경을 어떻게 구성하는가"를 이해한다.

#### 포함할 내용

**1.1 Microsoft Agent Framework란?**
- Semantic Kernel과 AutoGen의 후속 프레임워크임을 명시
- 두 프레임워크에서 가져온 개념:
  - AutoGen → 단순한 Agent 추상화, 멀티 에이전트 패턴
  - Semantic Kernel → 엔터프라이즈 기능 (세션 상태, 타입 안전성, 미들웨어, 텔레메트리)
- 추가된 개념: Graph 기반 Workflow, Durable Task 통합

**1.2 프레임워크 비교표**

| 기능 | Semantic Kernel | AutoGen | Agent Framework |
|---|---|---|---|
| Agent 추상화 | ChatCompletionAgent | AssistantAgent | AIAgent |
| Tool/Function | KernelFunction | FunctionTool | AIFunctionFactory |
| 멀티 에이전트 | AgentGroupChat | GroupChat / RoundRobin | Workflow + AsAIFunction |
| 상태 관리 | ChatHistory 직접 관리 | 내부 메시지 히스토리 | AgentSession |
| 미들웨어 | Filters | 없음 | Middleware 파이프라인 |
| Workflow | 없음 | 없음 | AgentWorkflowBuilder |
| 현재 상태 | GA | 유지보수 모드 | Public Preview |

**1.3 아키텍처 개요**
- 텍스트 다이어그램으로 구조 설명:
  ```
  [사용자 입력]
       ↓
  [Middleware Pipeline]  ← 로깅, 인증, 레이트 리밋 등
       ↓
  [AIAgent]
    ├─ instructions (시스템 프롬프트)
    ├─ tools (Function, MCP, 다른 Agent)
    └─ LLM Client (Azure OpenAI)
           ↓
      [Tool 호출 루프]  ← ReAct 패턴
           ↓
  [AgentSession]  ← 대화 히스토리 유지
       ↓
  [응답 반환]
  ```

**1.4 사전 요구사항**
- .NET 10 SDK 설치 확인: `dotnet --version`
- Azure CLI 설치 및 로그인: `az login`
- Azure OpenAI 리소스 필요 항목:
  - Endpoint: `AZURE_OPENAI_ENDPOINT`
  - Deployment Name: `AZURE_OPENAI_DEPLOYMENT_NAME` (예: `gpt-4o-mini`)
  - 임베딩 Deployment: `AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME` (5장 RAG에서 필요)

**1.5 프로젝트 생성 및 패키지 설치**

```bash
dotnet new console -n AgentSamples -f net10.0
cd AgentSamples

# 핵심 패키지
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity

# RAG용 (5장)
dotnet add package Microsoft.Extensions.VectorData.InMemory --prerelease
dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease

# 관측성용 (7장)
dotnet add package OpenTelemetry.Exporter.Console
```

**1.6 환경 변수 설정 방법**
- `dotnet user-secrets` 사용법 설명 (로컬 개발)
- `appsettings.json` 방식과 차이점 간단히 언급

---

### Chapter 2: 첫 번째 Agent 만들기 (`docs/02-first-agent.md`)

#### 작성 목표
가장 단순한 Agent를 만들어 실행하고, 핵심 개념(`AIAgent`, `AsAIAgent`, `RunAsync`)을 이해한다.

#### 포함할 내용

**2.1 개념 설명: AIAgent란?**
- `AIAgent`는 LLM Client를 감싸는 래퍼(wrapper)
- `AsAIAgent()` 확장 메서드로 모든 LLM Client를 Agent로 변환
- `instructions`는 시스템 프롬프트 역할

**2.2 기본 Agent 코드 예제** (`samples/01_HelloAgent.cs`)
```csharp
// 1. AzureOpenAIClient 생성
// 2. GetChatClient()로 Chat 클라이언트 획득
// 3. AsAIAgent()로 Agent 변환
// 4. RunAsync()로 단일 응답 받기
// 5. RunStreamingAsync()로 스트리밍 응답 받기
```

**2.3 단일 응답 vs 스트리밍 응답 비교**
- `RunAsync()`: 전체 응답 대기 후 string 반환
- `RunStreamingAsync()`: `IAsyncEnumerable<string>`으로 토큰 단위 스트리밍
- 언제 무엇을 쓸지 가이드라인 제공

**2.4 에러 처리 패턴**
- `try/catch`로 `RequestFailedException` 처리
- Endpoint, 인증 오류 등 흔한 오류 사례 설명

**2.5 예제 코드 실행 방법**
```bash
cd samples
AZURE_OPENAI_ENDPOINT=https://... dotnet run --project AgentSamples.csproj
```

---

### Chapter 3: Function Calling / Tool Use (`docs/03-function-calling.md`)

#### 작성 목표
Agent가 외부 함수를 자동으로 호출하는 Function Calling 메커니즘을 이해하고 구현한다.

#### 포함할 내용

**3.1 개념 설명: Tool Use란?**
- LLM이 직접 답할 수 없는 작업(실시간 데이터, 외부 API, DB 조회 등)을 함수로 위임
- Agent Framework의 Tool 실행 흐름:
  ```
  사용자 메시지
       ↓
  LLM → "이 Tool을 호출해야겠다" 판단
       ↓
  AIFunctionFactory가 실제 C# 함수 호출
       ↓
  결과를 LLM에 다시 전달 (Tool Result)
       ↓
  LLM이 최종 응답 생성
  ```
- 이 루프를 ReAct (Reasoning + Acting) 패턴이라고 부름

**3.2 Tool 정의 방법: `[Description]` 어트리뷰트**
- `System.ComponentModel.Description` 어트리뷰트로 함수와 파라미터 설명 추가
- LLM이 이 설명을 읽고 언제 Tool을 호출할지 결정
- 설명을 잘 작성하는 것이 Tool 호출 정확도에 직접 영향

**3.3 `AIFunctionFactory.Create()` 사용법**
- 정적 메서드로 Tool 등록
- 인스턴스 메서드로 Tool 등록 (클래스 내 메서드)
- 비동기 Tool 등록 (`async Task<T>`)

**3.4 예제 코드** (`samples/02_AddTools.cs`)
- 날씨 조회 Tool (목업 데이터)
- 현재 시각 반환 Tool
- 계산기 Tool (덧셈/곱셈)
- 세 가지 Tool을 가진 Agent 실행

**3.5 Tool 디버깅 팁**
- 어떤 Tool이 언제 호출됐는지 확인하는 방법
- Tool 파라미터 타입 매핑 규칙 (C# Type → JSON Schema)
- Tool 호출 횟수 제한 설정

**3.6 주의사항**
- Tool 정의 시 `[Description]` 없으면 LLM이 Tool 목적을 모름
- Azure OpenAI Chat Completion client는 일부 Tool 타입 미지원 (지원 행렬 표 포함)

---

### Chapter 4: 메모리 & 세션 관리 (`docs/04-memory-session.md`)

#### 작성 목표
Agent가 대화 맥락을 기억하는 방법(`AgentSession`)과 커스텀 메모리를 주입하는 방법을 이해한다.

#### 포함할 내용

**4.1 개념 설명: Stateless vs Stateful Agent**
- 기본 `RunAsync()`는 stateless: 호출할 때마다 새로운 대화
- `AgentSession`을 사용하면 stateful: 이전 대화를 기억
- 실제 사용 사례: 챗봇, 고객 지원 Agent

**4.2 AgentSession 기본 사용법** (`samples/03_MultiTurn.cs`)
- `agent.CreateSessionAsync()`로 세션 생성
- 세션을 `RunAsync()` 두 번째 인자로 전달
- 내부적으로 ChatHistory를 관리하는 방식 설명

**4.3 세션 ID와 지속성**
- 세션 ID를 통한 세션 관리 개념
- 현재 InMemory 세션의 한계 (프로세스 종료 시 소멸)
- 향후 Durable Task 통합으로 영속적 세션 가능성 언급

**4.4 Context Provider 패턴** (`samples/04_Memory.cs`)
- `IContextProvider` 인터페이스 역할: 모든 Agent 호출 전/후에 실행되는 훅
- `before_run`: 추가 컨텍스트/지시사항 주입
- `after_run`: 응답에서 정보 추출하여 저장
- 예제: 사용자 이름 기억 Provider 구현

**4.5 Context Provider와 Session의 관계**
- Session: 대화 히스토리(메시지 목록) 관리
- Context Provider: 매 호출마다 동적으로 컨텍스트 추가
- 둘을 함께 사용하는 패턴

**4.6 고급 패턴: 여러 Context Provider 체인**
- 사용자 프로파일 Provider
- 도구 실행 로그 Provider
- Provider 실행 순서 규칙

---

### Chapter 5: RAG - 검색 증강 생성 (`docs/05-rag.md`)

#### 작성 목표
외부 문서를 벡터화하여 Agent가 질문에 관련 문서를 검색하고 답변하는 RAG 패턴을 구현한다.

#### 포함할 내용

**5.1 개념 설명: RAG란?**
- LLM의 한계: 학습 데이터 기준일 이후 정보 없음, 특정 도메인 문서 부재
- RAG 3단계 파이프라인:
  ```
  [문서 준비]  → 텍스트 추출 → 청크 분할 → 임베딩 → Vector DB 저장
  [쿼리 처리]  → 쿼리 임베딩 → 유사도 검색 → 관련 청크 추출
  [답변 생성]  → 청크를 컨텍스트로 프롬프트에 주입 → LLM이 답변 생성
  ```
- RAG vs Fine-tuning 비교 (언제 RAG가 적합한가)

**5.2 `Microsoft.Extensions.VectorData` 개요**
- .NET AI 생태계의 공통 Vector Data 추상화 레이어
- `IVectorStore`, `IVectorStoreRecordCollection<TKey, TRecord>` 인터페이스
- InMemory, Azure AI Search, Redis 등 플러그인 방식으로 교체 가능

**5.3 InMemory Vector Store 구현** (`samples/05_RAG.cs`)

단계별 코드:
1. 문서 데이터 모델 정의 (`[VectorStoreRecordData]`, `[VectorStoreRecordVector]`)
2. InMemory Vector Store 초기화
3. 임베딩 모델 설정 (Azure OpenAI Embeddings)
4. 문서 청크 생성 및 벡터 저장
5. 유사도 검색 함수 구현
6. Agent Tool로 검색 함수 등록
7. Agent가 자동으로 관련 문서 검색 후 답변

**5.4 임베딩 모델 설정**
- Azure OpenAI `text-embedding-3-small` 또는 `text-embedding-ada-002` 사용
- 임베딩 차원 수 설정 주의사항
- 환경 변수: `AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME`

**5.5 RAG 품질 개선 팁**
- 청크 크기 선택 (너무 작으면 컨텍스트 부족, 너무 크면 노이즈 증가)
- 검색 결과 수 (`top: 3` vs `top: 5`)
- 메타데이터 필터링으로 관련성 향상

**5.6 Production에서의 Vector Store 교체**
- InMemory → Azure AI Search로 교체하는 코드 변경 최소화 예시
- 왜 추상화 레이어가 중요한지 설명

---

### Chapter 6: 멀티 에이전트 패턴 (`docs/06-multi-agent.md`)

#### 작성 목표
여러 Agent가 협력하는 패턴을 이해하고, `AsAIFunction()` 위임 방식과 Workflow 기반 방식을 모두 구현한다.

#### 포함할 내용

**6.1 멀티 에이전트가 필요한 이유**
- 단일 Agent의 한계: 너무 많은 책임, 컨텍스트 윈도우 한계
- 역할 분리로 각 Agent가 전문성 가짐
- 병렬 실행으로 성능 향상 가능

**6.2 패턴 1: Agent as Tool (`AsAIFunction()`)** (`samples/06_MultiAgent.cs` 일부)
- 핵심 개념: 내부 Agent를 Tool로 변환하여 외부 Agent에 제공
- 사용 시나리오: "필요할 때 위임"하는 유연한 조합
- 예제: Research Agent + Summary Agent + Master Agent

```csharp
// Research Agent 정의
AIAgent researchAgent = ...AsAIAgent(name: "ResearchAgent", ...);

// Summary Agent 정의  
AIAgent summaryAgent = ...AsAIAgent(name: "SummaryAgent", ...);

// Master Agent가 두 Agent를 Tool로 사용
AIAgent masterAgent = ...AsAIAgent(
    tools: [researchAgent.AsAIFunction(), summaryAgent.AsAIFunction()]
);
```

- `AsAIFunction()` 파라미터: `name`, `description`, `argName`, `argDescription`
- LLM이 어떤 Sub-Agent를 언제 호출할지 자동 결정하는 방식

**6.3 패턴 2: Workflow 기반 멀티 에이전트 (`AgentWorkflowBuilder`)**

핵심 개념 설명:
- **Executor**: Workflow의 처리 단위 (Agent 또는 일반 함수 모두 가능)
- **Edge**: Executor 간 연결 (데이터 흐름 정의)
- **Handler**: Executor 내에서 실제 로직이 있는 메서드 (`[Handler]` 어트리뷰트)
- **WorkflowContext**: 다음 Executor로 메시지 전달 / 최종 출력 반환

Executor 정의 방법 두 가지:
```csharp
// 방법 1: 클래스 기반
class MyExecutor : Executor {
    [Handler]
    public async Task Handle(string input, WorkflowContext<string> ctx) { ... }
}

// 방법 2: 함수 기반 (static method)
[Executor(Id = "my_step")]
static async Task MyStep(string input, WorkflowContext<Never, string> ctx) { ... }
```

**6.4 Sequential Workflow 예제** (`samples/06_MultiAgent.cs`)
```
[입력] → [전처리 Executor] → [AI Agent Executor] → [후처리 Executor] → [출력]
```
- 블로그 포스트 생성 파이프라인 구현
  1. `TopicExpander`: 주제를 상세 요구사항으로 확장
  2. `ContentWriter` (AI Agent): 실제 블로그 내용 작성
  3. `Formatter`: 마크다운 포맷 정리

**6.5 Conditional Routing 예제**
- Edge에 조건 추가: 분기 처리
```csharp
.AddEdge(classifierExecutor, urgentHandler, condition: msg => msg.Contains("긴급"))
.AddEdge(classifierExecutor, normalHandler, condition: msg => !msg.Contains("긴급"))
```

**6.6 패턴 비교: AsAIFunction vs Workflow**

| 기준 | AsAIFunction | Workflow |
|---|---|---|
| 제어 방식 | LLM이 자율 결정 | 개발자가 명시적 정의 |
| 실행 흐름 | 동적, 예측 불가 | 정적, 예측 가능 |
| 디버깅 난이도 | 어려움 | 쉬움 |
| 적합한 경우 | 탐색적 작업 | 비즈니스 프로세스 |

---

### Chapter 7: 미들웨어 & 관측성 (`docs/07-middleware-observability.md`)

#### 작성 목표
Agent 동작을 가로채는 미들웨어를 구현하고, OpenTelemetry로 Agent 실행을 추적한다.

#### 포함할 내용

**7.1 미들웨어 개념**
- HTTP 미들웨어(ASP.NET Core)와 동일한 개념을 Agent에 적용
- Agent 실행 파이프라인:
  ```
  [입력] → [미들웨어1] → [미들웨어2] → ... → [AIAgent 핵심 로직] → ... → [출력]
  ```
- 미들웨어로 할 수 있는 것:
  - 입력 로깅
  - 응답 캐싱
  - 에러 재시도
  - PII 마스킹
  - 레이트 리밋

**7.2 미들웨어 구현** (`samples/07_Middleware.cs`)

인터페이스:
```csharp
public interface IAgentMiddleware
{
    Task<AgentResponse> InvokeAsync(
        AgentRequest request,
        AgentMiddlewareDelegate next,
        CancellationToken cancellationToken = default);
}
```

예제 구현:
1. **로깅 미들웨어**: 요청/응답 시간, 토큰 수 기록
2. **재시도 미들웨어**: `RequestFailedException` 발생 시 지수 백오프로 재시도
3. **PII 마스킹 미들웨어**: 응답에서 이메일/전화번호 패턴 마스킹

미들웨어 등록:
```csharp
AIAgent agent = client.AsAIAgent(
    instructions: "...",
    middleware: [new LoggingMiddleware(), new RetryMiddleware()]
);
```

**7.3 OpenTelemetry 통합**

설치:
```bash
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Extensions.Hosting
```

설정:
```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Microsoft.Agents.AI")  // Agent Framework ActivitySource
    .AddConsoleExporter()
    .Build();
```

추적되는 정보:
- Agent 이름, instructions
- Tool 호출 이름 및 파라미터
- LLM 호출 지연 시간
- 토큰 사용량 (prompt_tokens, completion_tokens)
- 오류 정보

**7.4 커스텀 Span 추가**
- 비즈니스 로직에 직접 OpenTelemetry Span 추가하는 방법
- 미들웨어 내에서 Activity 생성 패턴

**7.5 구조화된 로깅 (`ILogger` 연동)**
- `Microsoft.Extensions.Logging`과 통합
- Agent Framework 내장 로그 레벨 설명
- 로그 필터링 설정

---

## 4. 공통 코드 패턴 및 규칙

### 4.1 Azure 인증 패턴
모든 예제는 `AzureCliCredential`을 사용한다 (로컬 개발 최적화).
```csharp
// 표준 초기화 패턴 (모든 예제에서 공통 사용)
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT 환경 변수를 설정하세요.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-4o-mini";

AIAgent agent = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsAIAgent(instructions: "...", name: "...");
```

### 4.2 파일 상단 주석 형식
각 `.cs` 샘플 파일 상단에 다음 형식으로 주석 추가:
```csharp
// ============================================================
// Chapter X: [챕터명]
// 파일: samples/0X_파일명.cs
// 관련 문서: docs/0X-챕터명.md
//
// 필요한 환경 변수:
//   - AZURE_OPENAI_ENDPOINT
//   - AZURE_OPENAI_DEPLOYMENT_NAME (기본값: gpt-4o-mini)
//
// 실행 방법:
//   dotnet run --project AgentSamples.csproj -- [챕터번호]
// ============================================================
```

### 4.3 에러 처리 공통 원칙
- 모든 예제에서 `try/catch`로 `RequestFailedException` 처리
- 환경 변수 누락 시 명확한 오류 메시지 출력
- `using` 문으로 리소스 해제

### 4.4 코드 스타일
- C# 10+ top-level statements 사용
- `var` 타입 추론 활용
- `async/await` 일관되게 사용 (`.Result` / `.Wait()` 금지)
- XML 문서 주석은 공개 API에만 최소한으로

---

## 5. NuGet 패키지 목록 및 버전

### samples/AgentSamples.csproj
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <!-- 핵심: Agent Framework + Azure OpenAI -->
    <PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.0.0-preview.*" />
    <PackageReference Include="Azure.AI.OpenAI" Version="2.*-beta.*" />
    <PackageReference Include="Azure.Identity" Version="1.*" />

    <!-- RAG용 Vector Store (5장) -->
    <PackageReference Include="Microsoft.Extensions.VectorData.InMemory" Version="9.*-preview.*" />
    <PackageReference Include="Microsoft.Extensions.AI.OpenAI" Version="9.*-preview.*" />

    <!-- 관측성 (7장) -->
    <PackageReference Include="OpenTelemetry" Version="1.*" />
    <PackageReference Include="OpenTelemetry.Exporter.Console" Version="1.*" />
  </ItemGroup>
</Project>
```

> **참고**: `Microsoft.Agents.AI.OpenAI`는 `Microsoft.Agents.AI`와 `Microsoft.Extensions.AI`를 의존성으로 포함하므로 별도 설치 불필요.

---

## 6. 작성 우선순위 및 일정

### 우선순위 (높음 → 낮음)

| 우선순위 | 파일 | 이유 |
|---|---|---|
| 1 | `README.md` | 가이드 진입점, 먼저 완성 필요 |
| 2 | `docs/01-introduction.md` | 환경 설정 없으면 나머지 불가 |
| 3 | `docs/02-first-agent.md` + `01_HelloAgent.cs` | 기반이 되는 핵심 개념 |
| 4 | `docs/03-function-calling.md` + `02_AddTools.cs` | 가장 많이 쓰이는 기능 |
| 5 | `docs/04-memory-session.md` + `03_MultiTurn.cs` + `04_Memory.cs` | 실용적인 챗봇 구현 |
| 6 | `docs/05-rag.md` + `05_RAG.cs` | 기술적 복잡도 높음 |
| 7 | `docs/06-multi-agent.md` + `06_MultiAgent.cs` | 고급 패턴 |
| 8 | `docs/07-middleware-observability.md` + `07_Middleware.cs` | 프로덕션 준비 |

### 작업 체크리스트

#### 사전 작업
- [x] 라이브러리 조사 (`microsoft/agent-framework`)
- [x] 공식 문서 확인 (`learn.microsoft.com/agent-framework`)
- [x] NuGet 패키지 목록 확인
- [x] Plan.md 작성 (현재 파일)

#### 문서 작성
- [ ] `README.md`
- [ ] `docs/01-introduction.md`
- [ ] `docs/02-first-agent.md`
- [ ] `docs/03-function-calling.md`
- [ ] `docs/04-memory-session.md`
- [ ] `docs/05-rag.md`
- [ ] `docs/06-multi-agent.md`
- [ ] `docs/07-middleware-observability.md`

#### 코드 샘플 작성
- [ ] `samples/AgentSamples.csproj`
- [ ] `samples/01_HelloAgent.cs`
- [ ] `samples/02_AddTools.cs`
- [ ] `samples/03_MultiTurn.cs`
- [ ] `samples/04_Memory.cs`
- [ ] `samples/05_RAG.cs`
- [ ] `samples/06_MultiAgent.cs`
- [ ] `samples/07_Middleware.cs`

#### 검증
- [ ] 코드 컴파일 확인 (`dotnet build`)
- [ ] 각 샘플 실행 확인 (Azure OpenAI 연결 필요)
- [ ] 문서 내 코드 스니펫과 실제 샘플 코드 일치 여부 확인
- [ ] 오탈자 및 링크 검토

---

## 7. 참고 자료

### 공식 문서
- MS Learn Agent Framework 문서: `https://learn.microsoft.com/agent-framework/`
- GitHub 레포지토리: `https://github.com/microsoft/agent-framework`
- GitHub .NET 샘플: `https://github.com/microsoft/agent-framework/tree/main/dotnet/samples`

### 관련 NuGet 프로필
- MicrosoftAgentFramework 프로필: `https://www.nuget.org/profiles/MicrosoftAgentFramework`
- `Microsoft.Agents.AI` (핵심): `https://www.nuget.org/packages/Microsoft.Agents.AI`
- `Microsoft.Agents.AI.OpenAI`: `https://www.nuget.org/packages/Microsoft.Agents.AI.OpenAI`

### 마이그레이션 가이드
- Semantic Kernel → Agent Framework: `https://learn.microsoft.com/agent-framework/migration-guide/from-semantic-kernel/`
- AutoGen → Agent Framework: `https://learn.microsoft.com/agent-framework/migration-guide/from-autogen/`

---

## 8. 기술적 주의사항 및 알려진 제약

### Public Preview 관련
- 패키지 버전: `1.0.0-preview.YYMMDD.N` 형식으로 빠르게 업데이트 중
- API가 GA 전에 변경될 수 있음 → 가이드에 버전 명시 필수
- Prerelease 패키지 사용 시 `--prerelease` 플래그 또는 `*-preview.*` 버전 지정 필요

### Azure OpenAI 클라이언트 선택
- **Chat Completion Client** (`GetChatClient()`): 기능 제한적이지만 안정적
- **Responses Client** (`GetOpenAIResponseClient()`): 더 많은 Tool 지원 (Code Interpreter, File Search, Web Search 등)
- 이 가이드는 **Chat Completion**을 기본으로 사용 (호환성 최우선)

### Tool 지원 행렬 주의
- Azure OpenAI Chat Completion은 File Search, Code Interpreter 미지원
- 해당 기능이 필요하면 Responses API 사용 필요
- 가이드의 Tool 예제는 Function Tools만 사용 (모든 Provider 공통 지원)

### InMemory Vector Store 한계
- 프로세스 종료 시 데이터 소멸 (프로덕션 부적합)
- 대용량 문서 처리 시 메모리 부족 가능
- 가이드에서 Production 전환 시 Azure AI Search 교체 방법 언급

### .NET 10 특이사항
- `#:package` 지시문으로 파일 기반 앱에서 NuGet 패키지 직접 참조 가능 (새 기능)
- 가이드는 표준 `.csproj` 방식 사용 (호환성 우선)
