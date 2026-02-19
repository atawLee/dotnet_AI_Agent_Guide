# .NET 10 / C# 으로 배우는 Microsoft Agent Framework

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4)
![Agent Framework](https://img.shields.io/badge/Microsoft%20Agent%20Framework-1.0.0--preview-blue)
![Azure OpenAI](https://img.shields.io/badge/Azure%20OpenAI-Chat%20Completions-0078D4)
![License](https://img.shields.io/badge/license-MIT-green)

> **⚠️ 주의**: Microsoft Agent Framework는 현재 **public preview** 상태입니다 (최신 버전: `1.0.0-preview.260212.1`).
> GA 전에 API가 변경될 수 있습니다. 각 챕터에서 해당 버전을 명시합니다.

---

## 소개

이 가이드는 **C# 중급 개발자**를 대상으로, Microsoft가 공식 출시한 [Agent Framework](https://github.com/microsoft/agent-framework)를 사용해 .NET 10 / C#으로 AI Agent를 구축하는 방법을 단계별로 설명합니다.

Python AI 생태계(LangChain, AutoGen 등)를 경험하고 .NET으로 전환을 검토 중이거나, Semantic Kernel을 사용해봤지만 더 단순한 Agent API를 원한다면 이 가이드가 적합합니다.

**이 가이드에서 배우는 것:**
- `AIAgent` 생성 및 Azure OpenAI 연동
- Function Calling으로 외부 도구 연결
- `AgentSession`을 이용한 멀티턴 대화
- InMemory Vector Store를 활용한 RAG 구현
- 멀티 에이전트 패턴 (`AsAIFunction`, `AgentWorkflowBuilder`)
- 미들웨어와 OpenTelemetry 관측성

---

## 기술 스택

| 항목 | 기술 | 버전 |
|---|---|---|
| 언어 / 런타임 | C# 13 / .NET 10 | GA |
| AI Agent 프레임워크 | Microsoft Agent Framework | `1.0.0-preview.*` |
| LLM 공급자 | Azure OpenAI | Chat Completions API |
| Vector Store | InMemory Vector Store | `9.*-preview.*` |
| 인증 | Azure CLI Credential | `az login` |
| 관측성 | OpenTelemetry + Console Exporter | `1.*` |

---

## 사전 요구사항

- [ ] **.NET 10 SDK** 설치: `dotnet --version` → `10.x.x` 확인
- [ ] **Azure CLI** 설치 및 로그인: `az login`
- [ ] **Azure OpenAI 리소스**: 아래 환경 변수 준비

```bash
# 필수 환경 변수
AZURE_OPENAI_ENDPOINT=https://<your-resource>.openai.azure.com/
AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini        # 또는 gpt-4o

# RAG 예제(5장)에 추가로 필요
AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME=text-embedding-3-small
```

---

## 빠른 시작 (Quick Start)

**1단계 — 레포지토리 클론**

```bash
git clone <this-repo-url>
cd blog_ai_agent_dotnet
```

**2단계 — 패키지 복원**

```bash
cd samples
dotnet restore
```

**3단계 — 환경 변수 설정**

```bash
# user-secrets 사용 (권장)
dotnet user-secrets init
dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://<your-resource>.openai.azure.com/"
dotnet user-secrets set "AZURE_OPENAI_DEPLOYMENT_NAME" "gpt-4o-mini"

# 또는 환경 변수로 직접 설정
export AZURE_OPENAI_ENDPOINT="https://<your-resource>.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
```

**4단계 — 첫 번째 샘플 실행**

```bash
dotnet run --project samples/AgentSamples.csproj -- 01
```

---

## 챕터 목차

| 챕터 | 문서 | 샘플 코드 | 핵심 개념 |
|---|---|---|---|
| 1장 | [소개 및 환경 설정](docs/01-introduction.md) | — | 프레임워크 비교, 아키텍처, 환경 구성 |
| 2장 | [첫 번째 Agent 만들기](docs/02-first-agent.md) | [01_HelloAgent.cs](samples/01_HelloAgent.cs) | `AIAgent`, `AsAIAgent()`, `RunAsync()`, 스트리밍 |
| 3장 | [Function Calling / Tool Use](docs/03-function-calling.md) | [02_AddTools.cs](samples/02_AddTools.cs) | `AIFunctionFactory`, `[Description]`, ReAct |
| 4장 | [메모리 & 세션 관리](docs/04-memory-session.md) | [03_MultiTurn.cs](samples/03_MultiTurn.cs), [04_Memory.cs](samples/04_Memory.cs) | `AgentSession`, Context Provider |
| 5장 | [RAG — 검색 증강 생성](docs/05-rag.md) | [05_RAG.cs](samples/05_RAG.cs) | Vector Store, 임베딩, 유사도 검색 |
| 6장 | [멀티 에이전트 패턴](docs/06-multi-agent.md) | [06_MultiAgent.cs](samples/06_MultiAgent.cs) | `AsAIFunction()`, `AgentWorkflowBuilder` |
| 7장 | [미들웨어 & 관측성](docs/07-middleware-observability.md) | [07_Middleware.cs](samples/07_Middleware.cs) | `IAgentMiddleware`, OpenTelemetry |

---

## 디렉토리 구조

```
blog_ai_agent_dotnet/
├── README.md                            # 이 파일
├── Plan.md                              # 작업 설계 문서
├── Task.md                              # 태스크 체크리스트
│
├── docs/                                # 챕터별 설명 문서
│   ├── 01-introduction.md
│   ├── 02-first-agent.md
│   ├── 03-function-calling.md
│   ├── 04-memory-session.md
│   ├── 05-rag.md
│   ├── 06-multi-agent.md
│   └── 07-middleware-observability.md
│
└── samples/                             # 실행 가능한 C# 코드
    ├── AgentSamples.csproj
    ├── 01_HelloAgent.cs
    ├── 02_AddTools.cs
    ├── 03_MultiTurn.cs
    ├── 04_Memory.cs
    ├── 05_RAG.cs
    ├── 06_MultiAgent.cs
    └── 07_Middleware.cs
```

---

## 참고 자료

- [Microsoft Agent Framework — GitHub](https://github.com/microsoft/agent-framework)
- [MS Learn — Agent Framework 문서](https://learn.microsoft.com/agent-framework/)
- [NuGet — Microsoft.Agents.AI.OpenAI](https://www.nuget.org/packages/Microsoft.Agents.AI.OpenAI)
- [Semantic Kernel → Agent Framework 마이그레이션](https://learn.microsoft.com/agent-framework/migration-guide/from-semantic-kernel/)
- [AutoGen → Agent Framework 마이그레이션](https://learn.microsoft.com/agent-framework/migration-guide/from-autogen/)

---

## 라이선스

MIT License. 자세한 내용은 [LICENSE](LICENSE) 파일을 참조하세요.
