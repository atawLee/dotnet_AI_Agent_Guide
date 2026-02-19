# Task.md — 작업 태스크 목록

> 최초 작성: 2026-02-19  
> 기반 문서: `Plan.md`  
> 진행 상태 범례: `[ ]` 대기 · `[~]` 진행 중 · `[x]` 완료 · `[-]` 취소

---

## 전체 Phase 구성

| Phase | 이름 | 산출물 | 의존성 |
|---|---|---|---|
| **Phase 0** | 사전 조사 & 계획 | `Plan.md`, `Task.md` | 없음 |
| **Phase 1** | 프로젝트 골격 구성 | `README.md`, `samples/AgentSamples.csproj`, 디렉터리 구조 | Phase 0 |
| **Phase 2** | 기초 문서 작성 | `docs/01-introduction.md`, `docs/02-first-agent.md` | Phase 1 |
| **Phase 3** | 핵심 기능 코드 작성 | `01_HelloAgent.cs`, `02_AddTools.cs` | Phase 2 |
| **Phase 4** | 상태 관리 문서 & 코드 | `docs/03-function-calling.md`, `docs/04-memory-session.md`, `02_AddTools.cs`, `03_MultiTurn.cs`, `04_Memory.cs` | Phase 3 |
| **Phase 5** | RAG 문서 & 코드 | `docs/05-rag.md`, `05_RAG.cs` | Phase 4 |
| **Phase 6** | 멀티 에이전트 문서 & 코드 | `docs/06-multi-agent.md`, `06_MultiAgent.cs` | Phase 5 |
| **Phase 7** | 미들웨어 & 관측성 문서 & 코드 | `docs/07-middleware-observability.md`, `07_Middleware.cs` | Phase 6 |
| **Phase 8** | 최종 검증 & 마무리 | 빌드 확인, 문서-코드 정합성, 링크 검토 | Phase 7 |

---

## Phase 0 — 사전 조사 & 계획 수립

> 목표: 가이드에 필요한 기술 조사를 완료하고 작업 범위를 확정한다.

| # | 태스크 | 상태 | 비고 |
|---|---|---|---|
| 0-1 | `microsoft/agent-framework` GitHub 레포지토리 구조 파악 | `[x]` | 7.2k stars, .NET + Python 지원 |
| 0-2 | 공식 MS Learn 문서 (`learn.microsoft.com/agent-framework`) 검토 | `[x]` | Get Started 5단계 튜토리얼 확인 |
| 0-3 | NuGet 패키지 목록 및 최신 버전 확인 | `[x]` | `Microsoft.Agents.AI.OpenAI` v1.0.0-preview.260212.1 |
| 0-4 | Azure OpenAI 클라이언트 타입(Chat / Responses) 차이 정리 | `[x]` | Chat 기본 사용, Responses는 고급 Tool만 |
| 0-5 | Tool 지원 행렬 확인 (Provider × Tool Type) | `[x]` | Chat Completion: Function Tools만 지원 |
| 0-6 | `Plan.md` 작성 완료 | `[x]` | 8개 섹션, 챕터별 상세 명세 포함 |
| 0-7 | `Task.md` 작성 (현재 파일) | `[x]` | Phase 기반 순차 태스크 목록 |

---

## Phase 1 — 프로젝트 골격 구성

> 목표: 독자가 레포지토리를 클론하자마자 전체 구조를 파악하고 환경을 구성할 수 있도록 뼈대를 만든다.

### 1-A. 디렉터리 & 파일 구조 생성

| # | 태스크 | 상태 | 산출물 |
|---|---|---|---|
| 1-1 | `docs/` 디렉터리 생성 | `[ ]` | `docs/` |
| 1-2 | `samples/` 디렉터리 생성 | `[ ]` | `samples/` |
| 1-3 | `samples/AgentSamples.csproj` 생성 | `[ ]` | .NET 10 Console 프로젝트, 전체 패키지 참조 포함 |

**`AgentSamples.csproj` 포함 패키지:**
- `Microsoft.Agents.AI.OpenAI` `1.0.0-preview.*`
- `Azure.AI.OpenAI` `2.*-beta.*`
- `Azure.Identity` `1.*`
- `Microsoft.Extensions.VectorData.InMemory` (RAG용)
- `Microsoft.Extensions.AI.OpenAI` (임베딩용)
- `OpenTelemetry.Exporter.Console` (관측성용)

### 1-B. README.md 작성

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 1-4 | 가이드 소개 및 목적 작성 | `[ ]` | 한 문단, 대상 독자 명시 |
| 1-5 | 기술 스택 뱃지/표 추가 | `[ ]` | .NET 10, Agent Framework, Azure OpenAI |
| 1-6 | 사전 요구사항 섹션 작성 | `[ ]` | .NET 10 SDK, Azure CLI, Azure OpenAI 리소스 |
| 1-7 | 빠른 시작 (Quick Start) 섹션 작성 | `[ ]` | 레포 클론 → 패키지 설치 → 환경 변수 → 첫 실행 4단계 |
| 1-8 | 챕터 목차 링크 테이블 작성 | `[ ]` | 7개 챕터 → `docs/` 링크 연결 |
| 1-9 | Public Preview 경고 배너 추가 | `[ ]` | `> ⚠️ Microsoft Agent Framework는 현재 public preview...` |
| 1-10 | 참고 자료 & 라이선스 섹션 추가 | `[ ]` | GitHub, MS Learn, NuGet 링크 |

---

## Phase 2 — 기초 문서 작성

> 목표: 독자가 "왜", "무엇을", "어떻게 설치하는지"를 이해하고 첫 Agent를 실행한다.

### 2-A. `docs/01-introduction.md` — 소개 및 환경 설정

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 2-1 | Microsoft Agent Framework 개요 작성 | `[ ]` | Semantic Kernel + AutoGen의 후속임을 설명, 배경 맥락 |
| 2-2 | 프레임워크 비교표 작성 | `[ ]` | Semantic Kernel / AutoGen / Agent Framework 3열 비교 (7개 항목) |
| 2-3 | 아키텍처 다이어그램(텍스트) 작성 | `[ ]` | 사용자 입력 → Middleware → AIAgent → Tool Loop → Session → 응답 |
| 2-4 | 사전 요구사항 체크리스트 작성 | `[ ]` | .NET 10, Azure CLI, 환경 변수 3종 |
| 2-5 | 프로젝트 생성 커맨드 블록 작성 | `[ ]` | `dotnet new console`, 각 패키지 `dotnet add package` |
| 2-6 | 환경 변수 설정 방법 (`dotnet user-secrets`) 작성 | `[ ]` | `user-secrets init`, `user-secrets set` 명령어 예시 |
| 2-7 | Public Preview 주의사항 섹션 추가 | `[ ]` | 버전 표기 방식, 변경 가능성 안내 |

### 2-B. `docs/02-first-agent.md` — 첫 번째 Agent 만들기

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 2-8 | `AIAgent` 개념 설명 작성 | `[ ]` | LLM Client 래퍼, `AsAIAgent()` 확장 메서드 역할 |
| 2-9 | `AzureOpenAIClient` → `GetChatClient()` → `AsAIAgent()` 연결 흐름 설명 | `[ ]` | 3줄 체인 설명, 각 단계 역할 |
| 2-10 | `RunAsync()` vs `RunStreamingAsync()` 비교 섹션 작성 | `[ ]` | 반환 타입, 사용 시나리오, 코드 스니펫 |
| 2-11 | 에러 처리 패턴 (흔한 오류 3가지) 작성 | `[ ]` | Endpoint 오류 / 인증 오류 / 모델 없음 |
| 2-12 | 샘플 코드 참조 링크 및 실행 방법 작성 | `[ ]` | `../samples/01_HelloAgent.cs` 링크, 실행 커맨드 |

---

## Phase 3 — 기초 샘플 코드 작성

> 목표: Phase 2 문서와 1:1 대응하는 실행 가능한 C# 코드를 작성한다.

### 3-A. `samples/01_HelloAgent.cs`

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 3-1 | 파일 상단 주석 블록 작성 | `[ ]` | 챕터명, 환경 변수, 실행 방법 명시 |
| 3-2 | 환경 변수 로드 및 검증 코드 작성 | `[ ]` | `AZURE_OPENAI_ENDPOINT`, `AZURE_OPENAI_DEPLOYMENT_NAME` |
| 3-3 | `AzureOpenAIClient` + `AsAIAgent()` 초기화 코드 작성 | `[ ]` | `AzureCliCredential` 사용 |
| 3-4 | `RunAsync()` 단일 응답 예제 코드 작성 | `[ ]` | 질문 1개, 콘솔 출력 |
| 3-5 | `RunStreamingAsync()` 스트리밍 예제 코드 작성 | `[ ]` | `await foreach`, `Console.Write(chunk)` |
| 3-6 | `try/catch` 에러 처리 코드 작성 | `[ ]` | `RequestFailedException`, 일반 `Exception` |

### 3-B. `samples/02_AddTools.cs`

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 3-7 | 날씨 조회 Tool 함수 작성 | `[ ]` | `[Description]` 어트리뷰트, 목업 데이터 반환 |
| 3-8 | 현재 시각 반환 Tool 함수 작성 | `[ ]` | `DateTime.Now` 포맷 반환 |
| 3-9 | 계산기 Tool 함수 작성 | `[ ]` | `Add(double a, double b)`, `Multiply(double a, double b)` |
| 3-10 | `AIFunctionFactory.Create()` 로 3개 Tool 등록 및 Agent 생성 | `[ ]` | `tools` 파라미터에 배열로 전달 |
| 3-11 | Tool 호출을 유발하는 질문 3개로 Agent 실행 코드 작성 | `[ ]` | 날씨/시각/계산 각 1회 |

---

## Phase 4 — 상태 관리 문서 & 코드 작성

> 목표: 멀티턴 대화와 메모리 패턴을 문서화하고 코드로 증명한다.

### 4-A. `docs/03-function-calling.md` — Function Calling 상세 문서

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 4-1 | Tool Use 개념 및 ReAct 패턴 설명 작성 | `[ ]` | 텍스트 플로우 다이어그램, Reasoning + Acting 루프 |
| 4-2 | `[Description]` 어트리뷰트 작성 가이드라인 작성 | `[ ]` | 좋은 설명 vs 나쁜 설명 예시 비교 |
| 4-3 | `AIFunctionFactory.Create()` 오버로드 설명 | `[ ]` | 정적/인스턴스/비동기 메서드 등록 방법 |
| 4-4 | Tool 파라미터 타입 매핑표 작성 | `[ ]` | C# `string/int/bool/DateTime/enum` → JSON Schema 대응 |
| 4-5 | Tool 디버깅 팁 섹션 작성 | `[ ]` | 호출 로그 확인, 파라미터 검증, 호출 횟수 제한 |
| 4-6 | Provider별 Tool 지원 행렬 표 삽입 | `[ ]` | Chat Completion / Responses / Assistants 비교 |

### 4-B. `docs/04-memory-session.md` — 메모리 & 세션 관리

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 4-7 | Stateless vs Stateful Agent 비교 설명 작성 | `[ ]` | 코드 없이 개념만, 각 사용 케이스 |
| 4-8 | `AgentSession` 기본 사용법 작성 | `[ ]` | `CreateSessionAsync()`, 세션 전달, 내부 동작 원리 |
| 4-9 | 세션 ID 관리 및 영속성 한계 섹션 작성 | `[ ]` | InMemory 한계, Durable Task 연동 가능성 언급 |
| 4-10 | Context Provider 인터페이스 설명 작성 | `[ ]` | `before_run` / `after_run` 훅 역할, 호출 순서 |
| 4-11 | 여러 Context Provider 체인 패턴 설명 작성 | `[ ]` | Provider 배열 순서 규칙, 실행 순서 다이어그램 |

### 4-C. `samples/03_MultiTurn.cs`

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 4-12 | `AgentSession` 생성 및 멀티턴 대화 코드 작성 | `[ ]` | 3턴 대화 (이름 알리기 → 날씨 → 이름 기억 확인) |
| 4-13 | 세션 없는 경우와 있는 경우 비교 출력 코드 작성 | `[ ]` | 같은 질문을 세션 유무로 각각 실행, 결과 비교 |

### 4-D. `samples/04_Memory.cs`

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 4-14 | `UserNameContextProvider` 클래스 구현 | `[ ]` | `before_run`에서 이름 주입, `after_run`에서 이름 추출 |
| 4-15 | Context Provider를 포함한 Agent 생성 코드 작성 | `[ ]` | `context_providers` 파라미터 |
| 4-16 | Provider 동작 확인용 3턴 시나리오 실행 코드 작성 | `[ ]` | 이름 모름 → 이름 알려줌 → 이름 확인 |

---

## Phase 5 — RAG 문서 & 코드 작성

> 목표: InMemory Vector Store를 사용한 완전한 RAG 파이프라인을 문서화하고 구현한다.

### 5-A. `docs/05-rag.md` — RAG 검색 증강 생성

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 5-1 | RAG 개념 및 필요성 설명 작성 | `[ ]` | LLM 한계 2가지, RAG가 해결하는 방식 |
| 5-2 | RAG 3단계 파이프라인 다이어그램 작성 | `[ ]` | 문서 준비 → 쿼리 처리 → 답변 생성, 각 단계 기술 설명 |
| 5-3 | RAG vs Fine-tuning 비교표 작성 | `[ ]` | 비용/속도/신선도/정확도 4가지 기준 비교 |
| 5-4 | `Microsoft.Extensions.VectorData` 추상화 설명 작성 | `[ ]` | `IVectorStore` 인터페이스, 플러그인 교체 방식 |
| 5-5 | `[VectorStoreRecordData]` / `[VectorStoreRecordVector]` 어트리뷰트 설명 작성 | `[ ]` | 데이터 모델 정의 방법, 필드별 역할 |
| 5-6 | 임베딩 모델 설정 및 차원 수 주의사항 작성 | `[ ]` | `text-embedding-3-small` 1536차원, `ada-002` 1536차원 |
| 5-7 | 청크 전략 및 RAG 품질 개선 팁 작성 | `[ ]` | 청크 크기 선택 기준, top-K 설정, 메타데이터 활용 |
| 5-8 | InMemory → Azure AI Search 교체 시 변경 범위 설명 작성 | `[ ]` | 의존성 교체 1줄로 가능함을 코드로 보여줌 |

### 5-B. `samples/05_RAG.cs`

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 5-9 | `DocumentChunk` 레코드 모델 정의 | `[ ]` | `Id(Guid)`, `Content(string)`, `Source(string)`, `Embedding(float[])` |
| 5-10 | InMemory Vector Store 초기화 코드 작성 | `[ ]` | `InMemoryVectorStore`, `GetCollection<Guid, DocumentChunk>()` |
| 5-11 | 샘플 문서 5개 준비 (하드코딩) | `[ ]` | Agent Framework 관련 FAQ 형식 5문항 |
| 5-12 | Azure OpenAI 임베딩 클라이언트 설정 코드 작성 | `[ ]` | `AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME` 환경 변수 사용 |
| 5-13 | 문서 임베딩 생성 및 Vector Store 저장 코드 작성 | `[ ]` | `UpsertAsync()` 사용, 배치 처리 |
| 5-14 | `SearchDocumentsAsync()` Tool 함수 구현 | `[ ]` | 쿼리 임베딩 → `VectorizedSearchAsync()` → 상위 3개 반환 |
| 5-15 | RAG Agent 생성 및 질의 실행 코드 작성 | `[ ]` | Tool 등록, 3개 질문 실행 (관련 있는 것 2개 + 없는 것 1개) |

---

## Phase 6 — 멀티 에이전트 문서 & 코드 작성

> 목표: Agent 위임 패턴과 Workflow 기반 멀티 에이전트를 문서화하고 구현한다.

### 6-A. `docs/06-multi-agent.md` — 멀티 에이전트 패턴

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 6-1 | 멀티 에이전트 필요성 설명 작성 | `[ ]` | 단일 Agent 한계 3가지, 역할 분리 이점 |
| 6-2 | 패턴 1: `AsAIFunction()` 위임 방식 설명 작성 | `[ ]` | 개념, 코드 스니펫, 동작 원리, 파라미터 설명 |
| 6-3 | 패턴 2: `AgentWorkflowBuilder` 방식 설명 작성 | `[ ]` | Executor / Edge / Handler / WorkflowContext 개념 |
| 6-4 | Executor 정의 2가지 방법 설명 작성 | `[ ]` | 클래스 기반 vs 함수 기반, `[Handler]` 어트리뷰트 |
| 6-5 | Sequential Workflow 예제 다이어그램 작성 | `[ ]` | 입력 → 전처리 → AI → 후처리 → 출력 텍스트 플로우 |
| 6-6 | Conditional Routing (분기 처리) 설명 및 코드 스니펫 작성 | `[ ]` | `condition:` 람다 파라미터 설명 |
| 6-7 | `AsAIFunction` vs `Workflow` 비교표 작성 | `[ ]` | 제어방식/예측가능성/디버깅/적합 케이스 4항목 |

### 6-B. `samples/06_MultiAgent.cs`

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 6-8 | 패턴 1 구현: `ResearchAgent` + `SummaryAgent` + `MasterAgent` | `[ ]` | 각 Agent 정의, `AsAIFunction()` 변환, Master Agent 실행 |
| 6-9 | 패턴 2 구현: `TopicExpander` Executor 클래스 작성 | `[ ]` | `Executor` 상속, `[Handler]` 메서드, `SendMessageAsync()` |
| 6-10 | 패턴 2 구현: `ContentWriter` AI Agent Executor 작성 | `[ ]` | `AIAgent`를 Executor로 감싸는 패턴 |
| 6-11 | 패턴 2 구현: `Formatter` 함수 기반 Executor 작성 | `[ ]` | `[Executor(Id = "formatter")]`, `YieldOutputAsync()` |
| 6-12 | `AgentWorkflowBuilder`로 3단계 파이프라인 연결 및 실행 | `[ ]` | `.AddEdge()` 체인, `.Build()`, `RunAsync()` |
| 6-13 | Conditional Routing 예제 구현 (긴급/일반 분기) | `[ ]` | 조건 람다 포함 `AddEdge()`, 2가지 입력으로 실행 확인 |

---

## Phase 7 — 미들웨어 & 관측성 문서 & 코드 작성

> 목표: 프로덕션 준비를 위한 미들웨어 패턴과 OpenTelemetry 통합을 완성한다.

### 7-A. `docs/07-middleware-observability.md` — 미들웨어 & 관측성

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 7-1 | 미들웨어 개념 및 파이프라인 구조 설명 작성 | `[ ]` | ASP.NET Core 미들웨어와 비교, 실행 순서 다이어그램 |
| 7-2 | `IAgentMiddleware` 인터페이스 설명 및 구현 가이드 작성 | `[ ]` | `InvokeAsync` 시그니처, `next` 델리게이트 호출 패턴 |
| 7-3 | 로깅 미들웨어 코드 스니펫 및 설명 작성 | `[ ]` | `Stopwatch` 측정, 토큰 수 기록 방법 |
| 7-4 | 재시도 미들웨어 코드 스니펫 및 설명 작성 | `[ ]` | 지수 백오프, 최대 재시도 횟수, 재시도 대상 예외 |
| 7-5 | PII 마스킹 미들웨어 코드 스니펫 및 설명 작성 | `[ ]` | 이메일/전화번호 Regex 패턴, 마스킹 치환 |
| 7-6 | OpenTelemetry 패키지 설치 및 초기 설정 작성 | `[ ]` | `Sdk.CreateTracerProviderBuilder()`, `AddSource("Microsoft.Agents.AI")` |
| 7-7 | 추적되는 정보 목록 (Span 속성) 설명 작성 | `[ ]` | Agent명, Tool 호출, 지연 시간, 토큰 사용량, 오류 |
| 7-8 | 커스텀 Span 추가 방법 작성 | `[ ]` | `ActivitySource`, `StartActivity()`, 태그 추가 |
| 7-9 | `ILogger` 연동 및 로그 필터링 설정 작성 | `[ ]` | `LogLevel` 필터, Agent Framework 내장 로그 카테고리 |

### 7-B. `samples/07_Middleware.cs`

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 7-10 | `LoggingMiddleware` 클래스 구현 | `[ ]` | 요청/응답 시간 측정, 입력 메시지 길이, 응답 길이 출력 |
| 7-11 | `RetryMiddleware` 클래스 구현 | `[ ]` | `RequestFailedException` 캐치, 최대 3회, 1s/2s/4s 백오프 |
| 7-12 | `PiiMaskingMiddleware` 클래스 구현 | `[ ]` | 이메일 / 전화번호 Regex 마스킹, 적용 전후 출력 비교 |
| 7-13 | 세 미들웨어를 조합한 Agent 생성 코드 작성 | `[ ]` | `middleware: [logging, pii, retry]` 순서, 실행 순서 설명 |
| 7-14 | OpenTelemetry 설정 및 Agent 실행 코드 작성 | `[ ]` | `using var tracerProvider`, Console Exporter 출력 확인 |
| 7-15 | 커스텀 Span 포함 비즈니스 로직 예제 코드 작성 | `[ ]` | `ActivitySource.StartActivity("CustomOperation")` 사용 |

---

## Phase 8 — 최종 검증 & 마무리

> 목표: 모든 코드가 빌드되고, 문서와 코드가 일치하며, 독자가 처음부터 끝까지 따라할 수 있음을 보장한다.

### 8-A. 빌드 검증

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 8-1 | `dotnet restore` 실행 및 패키지 복원 확인 | `[ ]` | prerelease 패키지 포함 전체 복원 성공 여부 |
| 8-2 | `dotnet build` 실행 및 오류 0개 확인 | `[ ]` | 경고는 허용, 오류는 모두 수정 |
| 8-3 | `dotnet build --warnaserror` 실행 및 경고 정리 | `[ ]` | nullable 경고, obsolete API 경고 등 |

### 8-B. 문서-코드 정합성 검토

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 8-4 | 각 `docs/*.md`의 코드 스니펫이 `samples/*.cs`와 일치하는지 확인 | `[ ]` | 7개 챕터 × 코드 블록 교차 검토 |
| 8-5 | 환경 변수 이름이 문서와 코드 전체에서 일관되는지 확인 | `[ ]` | `AZURE_OPENAI_ENDPOINT` 등 3개 변수 |
| 8-6 | `README.md`의 Quick Start가 실제 동작하는지 단계별 확인 | `[ ]` | 새 디렉터리에서 처음부터 따라하기 |

### 8-C. 문서 품질 검토

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 8-7 | 모든 `docs/*.md` 내부 링크 (챕터 간 상호 참조) 유효성 확인 | `[ ]` | `[다음 챕터 →]` 링크, `samples/` 파일 링크 |
| 8-8 | `README.md` 목차 링크 유효성 확인 | `[ ]` | `docs/01-introduction.md` ~ `docs/07-...md` |
| 8-9 | 오탈자 및 어색한 표현 전체 검토 | `[ ]` | 한국어 맞춤법, 기술 용어 일관성 (예: "에이전트" vs "Agent") |
| 8-10 | 코드 주석이 영문/한국어 혼용 없이 일관되는지 확인 | `[ ]` | 샘플 코드 주석은 한국어 통일 |

### 8-D. 최종 정리

| # | 태스크 | 상태 | 세부 내용 |
|---|---|---|---|
| 8-11 | `Plan.md` 및 `Task.md` 체크리스트 최종 업데이트 | `[ ]` | 완료된 항목 `[x]` 표시 |
| 8-12 | `.gitignore` 추가 (빌드 산출물 제외) | `[ ]` | `bin/`, `obj/`, `.vs/`, `*.user`, `user-secrets` |
| 8-13 | 최종 디렉터리 구조가 `Plan.md` 레이아웃과 일치하는지 확인 | `[ ]` | `ls -R` 결과 vs Plan.md 트리 비교 |

---

## 태스크 요약 통계

| Phase | 태스크 수 | 완료 | 잔여 |
|---|---|---|---|
| Phase 0 | 7 | 7 | 0 |
| Phase 1 | 10 | 0 | 10 |
| Phase 2 | 12 | 0 | 12 |
| Phase 3 | 11 | 0 | 11 |
| Phase 4 | 16 | 0 | 16 |
| Phase 5 | 15 | 0 | 15 |
| Phase 6 | 13 | 0 | 13 |
| Phase 7 | 15 | 0 | 15 |
| Phase 8 | 13 | 0 | 13 |
| **합계** | **112** | **7** | **105** |

---

## 작업 진행 시 규칙

1. **순서 준수**: 각 Phase는 이전 Phase 완료 후 시작한다. Phase 내 태스크는 번호 순서대로 진행한다.
2. **상태 즉시 업데이트**: 태스크 완료 즉시 `[x]`로 변경한다. 중단 시 `[~]`로 표시한다.
3. **블로커 발생 시**: 해당 태스크 옆에 `⚠️` 표시 후 `Plan.md` 주의사항 섹션에 기록한다.
4. **코드 변경 시 문서도 동시 업데이트**: 코드와 문서는 항상 동기화 상태를 유지한다.
5. **API 변경 감지 시**: NuGet 버전 업데이트로 인한 API 변경은 해당 Phase의 모든 관련 태스크에 `⚠️`를 붙이고 일괄 수정한다.
