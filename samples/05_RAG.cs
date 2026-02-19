// ============================================================
// Chapter 5: RAG (Retrieval-Augmented Generation)
// 파일: samples/05_RAG.cs
// 관련 문서: docs/05-rag.md
//
// 흐름:
//   1) 문서 청크를 임베딩 → 메모리 내 List에 저장 (in-process)
//   2) 사용자 질문을 임베딩 → 코사인 유사도로 Top-K 청크 검색
//   3) 검색된 청크를 컨텍스트로 Agent에 전달 → 답변 생성
//
// 외부 벡터 DB 대신 직접 구현한 인메모리 검색을 사용합니다.
// 프로덕션에서는 Azure AI Search, Qdrant 등으로 교체하세요.
//
// 사용 패키지:
//   - Microsoft.Extensions.AI.OpenAI  (임베딩 생성)
//   - Microsoft.Agents.AI.OpenAI      (Agent)
//
// 실행 방법:
//   dotnet run --project AgentSamples.csproj -- 05
// ============================================================

using Azure;
using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;

public static class RAGSample
{
    // ── 인메모리 벡터 스토어 레코드 ──────────────────────────────
    private sealed record IndexedChunk(
        string Title,
        string Text,
        float[] Embedding);

    // ── 예시 문서 ────────────────────────────────────────────────
    // 실제 프로덕션에서는 PDF/DB에서 로드하고 청킹(chunking) 처리를 합니다.
    private static readonly (string Title, string Text)[] SampleDocs =
    [
        ("회사 소개",
            "Contoso Inc.는 2010년 서울에서 설립된 B2B SaaS 기업입니다. " +
            "현재 임직원 450명이며, 클라우드 기반 ERP 솔루션을 전문으로 합니다."),
        ("제품: CloudERP Pro",
            "CloudERP Pro는 Contoso의 주력 제품으로, 재무·HR·공급망 모듈을 포함합니다. " +
            "월정액 구독 모델(SaaS)이며 99.9% SLA를 보장합니다."),
        ("지원 정책",
            "표준 지원은 평일 09:00~18:00 KST이며, 프리미엄 플랜 고객은 24/7 지원을 받습니다. " +
            "평균 초기 응답 시간은 2시간 이내입니다."),
        ("가격 정책",
            "CloudERP Pro 기본 플랜은 월 500,000원(최대 10 사용자)부터 시작합니다. " +
            "엔터프라이즈 플랜은 사용자 수에 따라 별도 협의합니다."),
        ("보안 인증",
            "Contoso는 ISO 27001 및 SOC 2 Type II 인증을 보유하고 있으며, " +
            "모든 데이터는 대한민국 데이터센터 내 암호화 저장됩니다."),
    ];

    public static async Task RunAsync(IConfiguration config)
    {
        Console.WriteLine("=== 05: RAG (Retrieval-Augmented Generation) ===");
        Console.WriteLine();

        // ── 설정 로드 ────────────────────────────────────────────────
        var endpoint = config["AzureOpenAI:Endpoint"]
            ?? config["AZURE_OPENAI_ENDPOINT"]
            ?? throw new InvalidOperationException("AzureOpenAI:Endpoint가 설정되지 않았습니다.");

        var apiKey = config["AzureOpenAI:ApiKey"]
            ?? config["AZURE_OPENAI_API_KEY"]
            ?? throw new InvalidOperationException("AzureOpenAI:ApiKey가 설정되지 않았습니다.");

        var deploymentName = config["AzureOpenAI:DeploymentName"]
            ?? config["AZURE_OPENAI_DEPLOYMENT_NAME"]
            ?? "gpt-4o-mini";

        var embeddingDeployment = config["AzureOpenAI:EmbeddingDeploymentName"]
            ?? config["AZURE_OPENAI_EMBEDDING_DEPLOYMENT"]
            ?? "text-embedding-3-small";

        // ── Azure OpenAI 클라이언트 ──────────────────────────────────
        var azureClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey));

        // ── 임베딩 생성기 ────────────────────────────────────────────
        // AsIEmbeddingGenerator(): Microsoft.Extensions.AI 확장 메서드
        IEmbeddingGenerator<string, Embedding<float>> embedder =
            azureClient
                .GetEmbeddingClient(embeddingDeployment)
                .AsIEmbeddingGenerator();

        // ── 색인: 문서 청크 임베딩 후 메모리에 저장 ─────────────────
        Console.WriteLine("[1/3] 문서 색인 중...");
        var index = new List<IndexedChunk>();

        foreach (var (title, text) in SampleDocs)
        {
            var result = await embedder.GenerateAsync([text]);
            var vector = result[0].Vector.ToArray();
            index.Add(new IndexedChunk(title, text, vector));
            Console.WriteLine($"  색인 완료: [{title}]");
        }
        Console.WriteLine();

        // ── Agent 초기화 ─────────────────────────────────────────────
        AIAgent agent = azureClient
            .GetChatClient(deploymentName)
            .AsAIAgent(
                instructions:
                    "당신은 Contoso 고객 지원 에이전트입니다. " +
                    "반드시 제공된 컨텍스트(문서 발췌)만을 근거로 대답하세요. " +
                    "컨텍스트에 없는 내용은 '해당 정보를 찾을 수 없습니다'라고 답하세요.",
                name: "RAGAgent"
            );

        // ── 데모 질문 ────────────────────────────────────────────────
        var questions = new[]
        {
            "Contoso는 언제, 어디서 설립되었나요?",
            "CloudERP Pro의 SLA는 몇 퍼센트인가요?",
            "24시간 지원을 받으려면 어떤 플랜이 필요한가요?",
            "Contoso의 보안 인증은 무엇인가요?",
        };

        Console.WriteLine("[2/3] RAG 질의응답 시작...");
        Console.WriteLine("(시스템: 매 질문마다 관련 문서를 검색해 컨텍스트로 주입합니다)");
        Console.WriteLine();

        foreach (var question in questions)
        {
            var contextChunks = await RetrieveAsync(index, embedder, question, topK: 3);
            var prompt = BuildPrompt(question, contextChunks);

            Console.WriteLine($"Q: {question}");
            Console.Write("A: ");

            await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(prompt, session: null))
                Console.Write(update.Text);

            Console.WriteLine("\n");
        }

        // ── 대화형 모드 ──────────────────────────────────────────────
        Console.WriteLine("[3/3] 대화형 모드 ('quit' 입력 시 종료)");
        Console.WriteLine();

        while (true)
        {
            Console.Write("질문: ");
            var input = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(input)) continue;
            if (input.Equals("quit", StringComparison.OrdinalIgnoreCase)) break;

            var chunks = await RetrieveAsync(index, embedder, input, topK: 3);
            var userPrompt = BuildPrompt(input, chunks);

            Console.Write("답변: ");
            try
            {
                await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(userPrompt, session: null))
                    Console.Write(update.Text);
                Console.WriteLine("\n");
            }
            catch (RequestFailedException ex)
            {
                Console.Error.WriteLine($"\n[오류] Azure API 오류 [{ex.Status}]: {ex.Message}");
            }
        }

        Console.WriteLine("RAG 샘플을 종료합니다.");
    }

    // ── 헬퍼: 코사인 유사도 기반 Top-K 검색 ────────────────────
    private static async Task<List<string>> RetrieveAsync(
        List<IndexedChunk> index,
        IEmbeddingGenerator<string, Embedding<float>> embedder,
        string query,
        int topK)
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

    // ── 헬퍼: 코사인 유사도 계산 ────────────────────────────────
    private static float CosineSimilarity(float[] a, float[] b)
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

    // ── 헬퍼: RAG 프롬프트 조합 ─────────────────────────────────
    private static string BuildPrompt(string question, List<string> contextChunks)
    {
        var contextBlock = string.Join("\n\n", contextChunks);
        return
            $"""
            === 참고 문서 ===
            {contextBlock}

            === 질문 ===
            {question}
            """;
    }
}
