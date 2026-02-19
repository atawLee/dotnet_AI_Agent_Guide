# 1장: 소개 및 환경 설정

> **버전 정보**: 이 문서는 `Microsoft.Agents.AI.OpenAI` `1.0.0-preview.260212.1` 기준으로 작성되었습니다.
> Public preview 단계이므로 API가 GA 전에 변경될 수 있습니다.

---

## 1.1 Microsoft Agent Framework란?

Microsoft Agent Framework(`microsoft/agent-framework`)는 Microsoft가 공식 출시한 .NET + Python AI Agent 구축 프레임워크입니다. 2024년 말부터 public preview로 제공되며, 기존 Semantic Kernel과 AutoGen의 개념을 통합·발전시킨 후속 프레임워크입니다.

**두 프레임워크에서 가져온 핵심 개념:**

| 출처 | 가져온 개념 |
|---|---|
| **AutoGen** | 단순하고 직관적인 Agent 추상화, 멀티 에이전트 협력 패턴 |
| **Semantic Kernel** | 엔터프라이즈급 기능 (세션 상태, 타입 안전성, 미들웨어, 텔레메트리) |

**새롭게 추가된 개념:**
- Graph 기반 Workflow (`AgentWorkflowBuilder`)
- Durable Task 통합 (장기 실행 Agent)
- 통합된 멀티 에이전트 패턴 (`AsAIFunction()`)

---

## 1.2 프레임워크 비교

| 기능 | Semantic Kernel | AutoGen | **Agent Framework** |
|---|---|---|---|
| Agent 추상화 | `ChatCompletionAgent` | `AssistantAgent` | **`AIAgent`** |
| Tool / Function | `KernelFunction` | `FunctionTool` | **`AIFunctionFactory`** |
| 멀티 에이전트 | `AgentGroupChat` | `GroupChat` / `RoundRobin` | **`Workflow` + `AsAIFunction`** |
| 상태 관리 | `ChatHistory` 직접 관리 | 내부 메시지 히스토리 | **`AgentSession`** |
| 미들웨어 | Filters | 없음 | **Middleware 파이프라인** |
| Workflow | 없음 | 없음 | **`AgentWorkflowBuilder`** |
| 현재 상태 | GA | 유지보수 모드 | **Public Preview** |

**언제 Agent Framework를 선택하는가?**
- Semantic Kernel의 복잡한 설정 없이 간결한 Agent API를 원할 때
- AutoGen의 멀티 에이전트 패턴을 .NET 엔터프라이즈 환경에서 사용할 때
- 예측 가능한 비즈니스 프로세스를 Workflow로 표현하고 싶을 때

---

## 1.3 아키텍처 개요

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

**주요 컴포넌트 설명:**

| 컴포넌트 | 역할 |
|---|---|
| `AIAgent` | LLM Client를 감싸는 Agent 핵심 클래스 |
| `AgentSession` | 대화 히스토리(ChatHistory)를 관리하는 세션 |
| `AIFunctionFactory` | C# 함수를 LLM이 호출 가능한 Tool로 변환 |
| Middleware | Agent 실행 전/후에 가로채는 파이프라인 |
| `AgentWorkflowBuilder` | 여러 Executor를 연결하는 그래프 기반 Workflow |

---

## 1.4 사전 요구사항

### 필수 도구

**1. .NET 10 SDK**
```bash
dotnet --version
# 출력 예: 10.0.100
```
설치: https://dotnet.microsoft.com/download/dotnet/10.0

**2. Azure CLI**
```bash
az --version
az login   # 브라우저를 통해 Azure 계정으로 로그인
```
설치: https://docs.microsoft.com/cli/azure/install-azure-cli

### Azure OpenAI 리소스 준비

Azure Portal에서 Azure OpenAI 리소스를 생성하고 아래 정보를 준비합니다:

| 환경 변수 | 설명 | 예시 |
|---|---|---|
| `AZURE_OPENAI_ENDPOINT` | 리소스 Endpoint URL | `https://myresource.openai.azure.com/` |
| `AZURE_OPENAI_DEPLOYMENT_NAME` | Chat 모델 배포 이름 | `gpt-4o-mini` |
| `AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME` | 임베딩 모델 배포 이름 (5장 RAG에서 필요) | `text-embedding-3-small` |

> **팁**: 이 가이드는 `AzureCliCredential`을 사용합니다. API 키를 코드에 하드코딩하지 않으므로 `az login`만 되어 있으면 별도 키 관리가 필요 없습니다.

---

## 1.5 프로젝트 생성 및 패키지 설치

새 프로젝트를 처음부터 생성하는 경우 아래 명령을 실행합니다.

```bash
dotnet new console -n AgentSamples -f net10.0
cd AgentSamples

# 핵심 패키지: Agent Framework + Azure OpenAI
dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
dotnet add package Azure.AI.OpenAI --prerelease
dotnet add package Azure.Identity

# RAG용 Vector Store (5장에서 사용)
dotnet add package Microsoft.Extensions.VectorData.InMemory --prerelease
dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease

# 관측성 (7장에서 사용)
dotnet add package OpenTelemetry.Exporter.Console
```

> **참고**: `Microsoft.Agents.AI.OpenAI`는 `Microsoft.Agents.AI`와 `Microsoft.Extensions.AI`를 의존성으로 포함하므로 별도 설치가 불필요합니다.

이 가이드의 샘플 코드를 사용하는 경우, `samples/AgentSamples.csproj`에 모든 패키지가 이미 포함되어 있습니다:

```bash
cd samples
dotnet restore
```

---

## 1.6 환경 변수 설정

### 방법 1: dotnet user-secrets (권장 — 로컬 개발)

```bash
cd samples
dotnet user-secrets init
dotnet user-secrets set "AZURE_OPENAI_ENDPOINT" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AZURE_OPENAI_DEPLOYMENT_NAME" "gpt-4o-mini"
dotnet user-secrets set "AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME" "text-embedding-3-small"
```

`user-secrets`는 `~/.microsoft/usersecrets/` 디렉터리에 별도로 저장되므로 Git에 민감 정보가 커밋될 위험이 없습니다.

### 방법 2: 환경 변수 직접 설정 (macOS/Linux)

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME="text-embedding-3-small"
```

### 방법 3: 환경 변수 직접 설정 (Windows PowerShell)

```powershell
$env:AZURE_OPENAI_ENDPOINT = "https://your-resource.openai.azure.com/"
$env:AZURE_OPENAI_DEPLOYMENT_NAME = "gpt-4o-mini"
$env:AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME = "text-embedding-3-small"
```

---

## 1.7 Public Preview 주의사항

Microsoft Agent Framework는 현재 **public preview** 단계입니다. 실제 프로젝트 적용 시 아래 사항을 고려하세요:

- **버전 고정 권장**: `1.0.0-preview.*` 와일드카드 대신 `1.0.0-preview.260212.1`처럼 특정 버전을 명시하는 것이 안정적입니다.
- **API 변경 가능성**: public preview 중에는 breaking change가 발생할 수 있습니다. NuGet 릴리즈 노트를 주기적으로 확인하세요.
- **프로덕션 사용**: preview 버전의 프로덕션 적용은 Microsoft의 공식 GA 발표 이후 권장됩니다.
- **NuGet 버전 확인 방법**:
  ```bash
  dotnet add package Microsoft.Agents.AI.OpenAI --prerelease
  # 설치된 버전을 확인하여 가이드 버전과 비교
  ```

---

## 다음 단계

환경 설정이 완료되었다면 2장으로 넘어가 첫 번째 Agent를 만들어봅시다.

[2장: 첫 번째 Agent 만들기 →](02-first-agent.md)
