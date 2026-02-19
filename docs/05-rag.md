# 5장. RAG (Retrieval-Augmented Generation)

> **주의**: Microsoft Agent Framework는 현재 **public preview** (`1.0.0-preview.260212.1`) 상태입니다. API는 정식 출시 전에 변경될 수 있습니다.

## RAG란?

LLM은 학습 데이터에 없는 **사내 문서, 최신 정보, 도메인 특화 지식**을 알지 못합니다.  
RAG(Retrieval-Augmented Generation)는 이 한계를 보완합니다.

```
사용자 질문
    │
    ▼
[임베딩] 질문 → 벡터
    │
    ▼
[검색] 벡터 DB에서 유사 청크 Top-K 조회
    │
    ▼
[주입] 프롬프트에 청크 포함
    │
    ▼
[생성] LLM이 컨텍스트 기반 답변 생성
```

## 핵심 개념

| 개념 | 설명 |
|------|------|
| **임베딩(Embedding)** | 텍스트를 고차원 숫자 벡터로 변환. 의미적으로 유사한 텍스트는 벡터 공간에서 가깝게 위치 |
| **청킹(Chunking)** | 긴 문서를 LLM 컨텍스트 창에 맞게 작은 단위로 분할하는 작업 |
| **벡터 검색** | 질문 벡터와 문서 벡터 간 코사인 유사도를 계산해 가장 관련성 높은 청크 선택 |
| **컨텍스트 주입** | 검색된 청크를 프롬프트에 포함시켜 LLM에게 근거 자료로 제공 |

## 프로젝트 설정

`appsettings.local.json`에 임베딩 배포명을 추가합니다.

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

패키지는 추가 설치 없이 기존 `Microsoft.Extensions.AI.OpenAI`의 `AsIEmbeddingGenerator()` 확장 메서드를 사용합니다.

## 임베딩 생성

```csharp
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Azure;

var azureClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureKeyCredential(apiKey));

// EmbeddingClient → IEmbeddingGenerator<string, Embedding<float>>
IEmbeddingGenerator<string, Embedding<float>> embedder =
    azureClient
        .GetEmbeddingClient("text-embedding-3-small")
        .AsIEmbeddingGenerator();

// 텍스트 임베딩 생성
var results = await embedder.GenerateAsync(["안녕하세요, Contoso입니다."]);
float[] vector = results[0].Vector.ToArray();  // 1536차원 float 배열
```

## 인메모리 벡터 인덱스

이 샘플에서는 외부 벡터 DB 없이 `List<IndexedChunk>`에 벡터를 저장하고  
**코사인 유사도(Cosine Similarity)**로 직접 검색합니다.

```csharp
private sealed record IndexedChunk(
    string Title,
    string Text,
    float[] Embedding);

// 코사인 유사도
static float CosineSimilarity(float[] a, float[] b)
{
    float dot = 0f, normA = 0f, normB = 0f;
    for (int i = 0; i < a.Length; i++)
    {
        dot   += a[i] * b[i];
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB) + 1e-10f);
}
```

> **프로덕션 선택지**: 문서 수천 건 이상이면 외부 벡터 DB가 필요합니다.
> - **Azure AI Search** (`Azure.Search.Documents`) — Azure 네이티브
> - **Qdrant** (`Qdrant.Client`) — 오픈소스, 로컬/클라우드
> - **pgvector** (PostgreSQL 확장) — 기존 DB 활용

## 색인 단계

```csharp
var index = new List<IndexedChunk>();

foreach (var (title, text) in documents)
{
    var result = await embedder.GenerateAsync([text]);
    index.Add(new IndexedChunk(title, text, result[0].Vector.ToArray()));
}
```

실제 운영에서는 애플리케이션 시작 시 한 번만 색인하거나,  
문서 변경 시 증분 업데이트(upsert) 합니다.

## 검색 단계

```csharp
async Task<List<string>> RetrieveAsync(string query, int topK)
{
    var result = await embedder.GenerateAsync([query]);
    var queryVector = result[0].Vector.ToArray();

    return index
        .Select(chunk => (chunk, score: CosineSimilarity(queryVector, chunk.Embedding)))
        .OrderByDescending(x => x.score)
        .Take(topK)
        .Select(x => $"[{x.chunk.Title}] {x.chunk.Text}")
        .ToList();
}
```

## 프롬프트 조합 및 Agent 호출

```csharp
var contextChunks = await RetrieveAsync(question, topK: 3);
var contextBlock = string.Join("\n\n", contextChunks);

var prompt =
    $"""
    === 참고 문서 ===
    {contextBlock}

    === 질문 ===
    {question}
    """;

await foreach (var update in agent.RunStreamingAsync(prompt, session: null))
    Console.Write(update.Text);
```

Agent의 `instructions`에 **컨텍스트만 근거로 답변**하도록 명시하면  
할루시네이션(환각)을 효과적으로 억제할 수 있습니다.

```csharp
AIAgent agent = azureClient
    .GetChatClient(deploymentName)
    .AsAIAgent(
        instructions:
            "당신은 Contoso 고객 지원 에이전트입니다. " +
            "반드시 제공된 컨텍스트(문서 발췌)만을 근거로 대답하세요. " +
            "컨텍스트에 없는 내용은 '해당 정보를 찾을 수 없습니다'라고 답하세요.",
        name: "RAGAgent"
    );
```

## 실행

```bash
dotnet run -- 05
```

예상 출력:
```
=== 05: RAG (Retrieval-Augmented Generation) ===

[1/3] 문서 색인 중...
  색인 완료: [회사 소개]
  색인 완료: [제품: CloudERP Pro]
  ...

[2/3] RAG 질의응답 시작...
Q: Contoso는 언제, 어디서 설립되었나요?
A: Contoso Inc.는 2010년 서울에서 설립되었습니다.

Q: CloudERP Pro의 SLA는 몇 퍼센트인가요?
A: CloudERP Pro는 99.9% SLA를 보장합니다.
```

## 전체 코드

`samples/05_RAG.cs` 참조.

## RAG 품질 개선 팁

| 기법 | 설명 |
|------|------|
| **청크 크기 조정** | 너무 크면 노이즈 증가, 너무 작으면 컨텍스트 부족. 512~1024 토큰이 일반적 |
| **청크 오버랩** | 청크 경계에서 문맥이 끊기지 않도록 앞뒤 청크를 일부 겹침 |
| **하이브리드 검색** | 벡터 검색 + 키워드 검색(BM25) 결합으로 정확도 향상 |
| **Re-ranking** | Top-K 결과를 Cross-Encoder 모델로 재정렬 |
| **메타데이터 필터링** | 검색 전 날짜·카테고리 등으로 후보를 좁혀 관련성 향상 |

## 다음 단계

- [6장. 멀티 에이전트](./06-multi-agent.md) — 에이전트 간 협업 패턴
