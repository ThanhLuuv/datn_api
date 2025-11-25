# AI Search System - Documentation

## Tổng quan

Hệ thống AI Search sử dụng **Vector Similarity Search** (RAG - Retrieval-Augmented Generation) kết hợp với **LLM (Large Language Model)** để trả lời câu hỏi về đơn hàng, hóa đơn, khách hàng và sách trong hệ thống BookStore.

## Kiến trúc tổng thể

```
┌─────────────┐
│ User Query  │
└──────┬──────┘
       │
       ▼
┌─────────────────────────┐
│ 1. Generate Embedding   │ ← Gemini text-embedding-004
│    (Query → Vector)     │
└──────┬──────────────────┘
       │
       ▼
┌─────────────────────────┐
│ 2. Vector Similarity    │ ← Cosine Similarity
│    Search               │
└──────┬──────────────────┘
       │
       ▼
┌─────────────────────────┐
│ 3. Filter & Rank        │ ← Adaptive Threshold + Boost
│    Documents            │
└──────┬──────────────────┘
       │
       ▼
┌─────────────────────────┐
│ 4. Build Prompt         │ ← TopK Documents + Instructions
│    with Context         │
└──────┬──────────────────┘
       │
       ▼
┌─────────────────────────┐
│ 5. Call LLM             │ ← Gemini 2.5 Flash
│    (Generate Answer)    │
└──────┬──────────────────┘
       │
       ▼
┌─────────────────────────┐
│ 6. Extract & Return     │
│    Answer               │
└─────────────────────────┘
```

## Các thành phần chính

### 1. **AiSearchService** (`BookStore.Api/Services/AiSearchService.cs`)

Service chính xử lý toàn bộ logic AI search.

#### 1.1. Supported RefTypes

Hệ thống chỉ hỗ trợ các loại dữ liệu liên quan đến đơn hàng:

```csharp
- "order": Thông tin đơn hàng
- "order_line": Chi tiết từng dòng trong đơn
- "invoice": Hóa đơn (1-1 với Order)
- "book": Thông tin sách (để có context)
- "customer": Thông tin khách hàng (để có context)
```

#### 1.2. Quy trình Search (`SearchKnowledgeBaseAsync`)

**Bước 1: Validate & Normalize Query**
```csharp
- Kiểm tra query không rỗng
- Normalize query (trim, lowercase)
- Clamp topK (1-15)
```

**Bước 2: Auto-suggest RefTypes**
```csharp
- Nếu không có RefTypes → tự động suggest dựa trên keywords
- Ví dụ: "đơn hàng" → ["order", "order_line"]
- Ví dụ: "hóa đơn" → ["invoice", "order"]
- Ví dụ: "khách hàng" → ["customer", "order"]
```

**Bước 3: Load Documents từ DB**
```csharp
- Query AiDocuments table với RefTypes được chọn
- Kiểm tra available RefTypes trong DB
- Warning nếu RefTypes yêu cầu không có dữ liệu
```

**Bước 4: Generate Query Embedding**
```csharp
- Gọi Gemini Embedding API (text-embedding-004)
- Vector dimension: 768 (tùy model)
- Embedding là vector số biểu diễn ngữ nghĩa của text
```

**Bước 5: Calculate Similarity**
```csharp
- Parse embedding từ JSON cho mỗi document
- Tính Cosine Similarity với query embedding
- Formula: cos(θ) = (A · B) / (||A|| × ||B||)
- Kết quả: 0.0 - 1.0 (1.0 = hoàn toàn giống nhau)
```

**Bước 6: Adaptive Threshold**
```csharp
- Tính threshold động dựa trên distribution của similarities
- Strategy:
  * Nếu có nhiều documents similarity cao (>0.7) → threshold cao (0.4-0.65)
  * Nếu ít documents similarity cao → threshold thấp hơn (0.3-0.55)
  * Sử dụng percentile (p75, p50) và mean
- Lý do: Không dùng threshold cố định vì chất lượng documents khác nhau
```

**Bước 7: Filter & Boost**
```csharp
- Filter documents có similarity >= threshold
- Boost RefTypes phù hợp với query:
  * Query "đơn hàng" → boost order, order_line (+0.1 điểm)
  * Query "hóa đơn" → boost invoice, order (+0.1 điểm)
  * Query "khách hàng" → boost customer, order (+0.1 điểm)
- Boost documents mới (UpdatedAt gần đây) cho queries về "gần đây" (+0.05 điểm)
```

**Bước 8: Ranking & Selection**
```csharp
- Order by: BoostedSimilarity → Similarity → UpdatedAt → RefType
- Take topK documents (có thể tăng động nếu có nhiều matches)
- Dynamic TopK:
  * >20 matches và topK < 10 → tăng lên 10
  * >10 matches và topK < 8 → tăng lên 8
```

**Bước 9: Build Prompt for LLM**
```csharp
- System Prompt: Hướng dẫn AI về cách trả lời
- User Payload:
  * question: Query của user
  * language: vi/en
  * documents: TopK documents với rank, RefType, similarity, content
  * instructions: Hướng dẫn cụ thể về cách sử dụng documents
```

**Bước 10: Call LLM & Extract Answer**
```csharp
- Gọi Gemini Chat API (gemini-2.5-flash)
- Extract answer từ JSON response
- Parse metadata nếu có
- Return answer + metadata
```

### 2. **Rebuild Index** (`RebuildAiSearchIndexAsync`)

Quy trình index dữ liệu vào `AiDocuments` table:

**Bước 1: Build Document Seeds**
```csharp
- Query dữ liệu từ các bảng: Orders, OrderLines, Invoices, Books, Customers
- Build content text cho mỗi document với format:
  * Loại dữ liệu: ORDER/ORDER_LINE/INVOICE/BOOK/CUSTOMER
  * Các trường thông tin quan trọng
  * Format tiền tệ, ngày tháng theo VietnameseCulture
- Ví dụ ORDER:
  Loại dữ liệu: ORDER
  OrderId: 123
  Khách hàng: Nguyễn Văn A (ID 5)
  Trạng thái: Đã giao
  Ngày đặt: 22/01/2025 10:30
  Tổng giá trị: 500,000 VNĐ
  ...
```

**Bước 2: Generate Embeddings**
```csharp
- Với mỗi seed:
  * Gọi Gemini Embedding API
  * Serialize embedding vector thành JSON
  * Delay 200ms giữa các requests (tránh rate limit)
- Embedding vector: Array<float> (768 dimensions)
```

**Bước 3: Save to Database**
```csharp
- Insert hoặc Update AiDocuments
- Batch save mỗi 20 documents
- Set UpdatedAt = DateTime.UtcNow
```

### 3. **Cosine Similarity Algorithm**

```csharp
private static double CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
{
    if (a.Count == 0 || b.Count == 0 || a.Count != b.Count)
        return 0;

    // Tính dot product
    double dot = 0;
    for (var i = 0; i < a.Count; i++)
        dot += a[i] * b[i];
    
    // Tính norms
    double normA = 0, normB = 0;
    for (var i = 0; i < a.Count; i++)
    {
        normA += a[i] * a[i];
        normB += b[i] * b[i];
    }
    
    if (normA == 0 || normB == 0)
        return 0;
    
    // Cosine similarity
    return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
}
```

**Ý nghĩa:**
- Giá trị từ -1 đến 1 (thường 0 đến 1 với embeddings)
- **1.0** = hoàn toàn giống nhau về ngữ nghĩa
- **0.0** = không liên quan
- **>0.7** = rất liên quan
- **0.3-0.7** = liên quan vừa phải
- **<0.3** = ít liên quan

**Tại sao dùng Cosine Similarity?**
- Không phụ thuộc vào độ dài vector (magnitude)
- Chỉ so sánh hướng (direction) của vector → phù hợp cho semantic search
- Embeddings đã được normalize nên cosine similarity hiệu quả

### 4. **Adaptive Threshold Algorithm**

```csharp
private static double CalculateAdaptiveThreshold(List<double> similarities)
{
    if (similarities.Count == 0) return 0.3; // Default
    if (similarities.Count == 1) return Math.Max(0.3, similarities[0] * 0.8);
    
    var sorted = similarities.OrderByDescending(s => s).ToList();
    var count = sorted.Count;
    
    // Tính các percentile
    var p75 = sorted[Math.Max(0, Math.Min(count - 1, (int)(count * 0.25))];
    var p50 = sorted[Math.Max(0, Math.Min(count - 1, (int)(count * 0.50))];
    var mean = sorted.Average();
    
    var highSimilarityCount = sorted.Count(s => s >= 0.7);
    
    if (highSimilarityCount >= 5)
        return Math.Clamp(p75, 0.4, 0.65);  // Nhiều documents tốt → threshold cao
    else if (highSimilarityCount >= 2)
        return Math.Clamp(p50, 0.35, 0.6); // Một số documents tốt → dùng median
    else
        return Math.Clamp(mean * 0.9, 0.3, 0.55); // Ít documents tốt → dùng mean
}
```

**Lý do dùng Adaptive Threshold:**
- **Không dùng threshold cố định** (0.1 quá thấp → nhiều noise, 0.7 quá cao → ít results)
- **Tự động điều chỉnh** dựa trên chất lượng của documents
- **Đảm bảo** lấy đủ documents nhưng không quá nhiều noise
- **Phù hợp** với các queries khác nhau (có query dễ match, có query khó match)

### 5. **RefType Boosting**

```csharp
private static List<string> GetPrioritizedRefTypesForQuery(string queryLower)
{
    var prioritized = new List<string>();
    
    if (queryLower.Contains("đơn hàng") || queryLower.Contains("order"))
        prioritized.Add("order");
        prioritized.Add("order_line");
    
    if (queryLower.Contains("hóa đơn") || queryLower.Contains("invoice"))
        prioritized.Add("invoice");
        prioritized.Add("order");
    
    if (queryLower.Contains("khách hàng") || queryLower.Contains("customer"))
        prioritized.Add("customer");
        prioritized.Add("order");
    
    // Boost score: +0.1 điểm similarity
    return prioritized;
}
```

**Lý do:**
- Đảm bảo documents phù hợp được ưu tiên
- Ví dụ: Query "hóa đơn" sẽ ưu tiên invoice documents hơn book documents
- Boost giúp documents quan trọng được chọn ngay cả khi similarity hơi thấp

### 6. **Auto-suggest RefTypes**

```csharp
private static List<string> SuggestRefTypesFromQuery(string query)
{
    var keywordMappings = new Dictionary<string, List<string>>
    {
        { "order", ["đơn hàng", "order", "đơn bán", ...] },
        { "invoice", ["hóa đơn", "invoice", "thanh toán", ...] },
        { "customer", ["khách hàng", "customer", ...] },
        { "book", ["sách", "book", ...] }
    };
    
    // Tìm keywords trong query → suggest RefTypes
    // Nếu không tìm thấy → trả về tất cả RefTypes
}
```

**Lý do:**
- User không cần chỉ định RefTypes thủ công
- Hệ thống tự động detect intent từ query
- Giảm complexity cho frontend

## Database Schema

### AiDocuments Table

```sql
CREATE TABLE ai_documents (
    id BIGINT PRIMARY KEY AUTO_INCREMENT,
    ref_type VARCHAR(100) NOT NULL,      -- order, order_line, invoice, book, customer
    ref_id VARCHAR(120) NOT NULL,          -- OrderId, InvoiceId, ISBN, CustomerId
    content TEXT NOT NULL,                -- Text content để search
    embedding_json TEXT NOT NULL,         -- JSON array của embedding vector
    updated_at DATETIME NOT NULL,
    INDEX idx_ref_type (ref_type),
    INDEX idx_ref_id (ref_id)
);
```

**Lưu ý:**
- `embedding_json` lưu dạng JSON array: `[0.123, 0.456, ...]`
- `content` là text đã format, không lưu raw data
- `updated_at` dùng để rank documents mới hơn

## Prompt Engineering

### System Prompt

```
Bạn là trợ lý AI chuyên về đơn hàng của hệ thống quản lý nhà sách BookStore.
Nhiệm vụ chính: Trả lời các câu hỏi về ĐƠN HÀNG, HÓA ĐƠN, KHÁCH HÀNG và SÁCH trong đơn hàng.

QUY TẮC:
1. Chỉ trả lời dựa trên DOCUMENTS được cung cấp
2. Nếu không đủ dữ liệu → trả lời rõ ràng
3. Trình bày ngắn gọn, rõ ràng, ưu tiên tiếng Việt
4. Dùng bullet points khi liệt kê
5. Luôn trích dẫn: OrderId, InvoiceId, ISBN, CustomerId

CÁC LOẠI DOCUMENTS:
- ORDER: Thông tin đơn hàng
- ORDER_LINE: Chi tiết từng dòng trong đơn
- INVOICE: Hóa đơn (1-1 với Order)
- BOOK: Thông tin sách
- CUSTOMER: Thông tin khách hàng

CÁCH TRẢ LỜI:
- Câu hỏi về 'đơn hàng gần nhất' → Tìm ORDER có updatedAt/PlacedAt gần nhất
- Câu hỏi về 'hóa đơn' → Tìm INVOICE
- Câu hỏi về 'khách hàng nào mua nhiều nhất' → Dùng CUSTOMER documents
...
```

### User Payload Structure

```json
{
  "question": "khách hàng nào mua nhiều nhất",
  "language": "vi",
  "documents": [
    {
      "rank": 1,
      "refType": "customer",
      "refId": "5",
      "similarity": 0.8234,
      "updatedAt": "2025-01-22T10:30:00Z",
      "updatedAtFormatted": "22/01/2025 10:30",
      "content": "Loại dữ liệu: CUSTOMER\nCustomerId: 5\nHọ tên: Hoàng Minh Em\n..."
    }
  ],
  "instructions": [
    "Chỉ dùng thông tin trong documents",
    "Khi câu hỏi về 'khách hàng' → tìm RefType = 'customer'",
    "Luôn trích dẫn CustomerId, OrderId khi có"
  ]
}
```

## Workflow chi tiết

### 1. Indexing Workflow

```
Admin gọi API /api/ai/search/reindex
    ↓
BuildAiDocumentSeedsAsync()
    ├─ Query Orders (maxOrders = 1000)
    ├─ Query OrderLines (maxOrders * 10)
    ├─ Query Invoices (maxOrders)
    ├─ Query Books (maxBooks = 1000)
    └─ Query Customers (maxCustomers = 500)
    ↓
Với mỗi seed:
    ├─ Build content text (format chuẩn)
    ├─ Gọi Gemini Embedding API
    ├─ Serialize embedding → JSON
    └─ Save vào AiDocuments
    ↓
Return số documents đã index
```

### 2. Search Workflow

```
User Query: "khách hàng nào mua nhiều nhất"
    ↓
[1] Validate & Normalize
    ├─ Trim query
    └─ Clamp topK (1-15)
    ↓
[2] Auto-suggest RefTypes
    └─ Detect keywords → ["customer", "order"]
    ↓
[3] Load Documents
    ├─ Query DB: refType IN ['customer', 'order']
    └─ Check available RefTypes
    ↓
[4] Generate Query Embedding
    └─ Gemini Embedding API → Vector[768]
    ↓
[5] Calculate Similarity
    ├─ Parse embedding từ JSON cho mỗi document
    ├─ Cosine Similarity với query embedding
    └─ Collect all similarities
    ↓
[6] Adaptive Threshold
    ├─ Calculate distribution
    ├─ Determine threshold (ví dụ: 0.45)
    └─ Filter documents (similarity >= 0.45)
    ↓
[7] Boost & Rank
    ├─ Boost customer documents (+0.1)
    ├─ Boost documents mới (+0.05)
    └─ Rank: BoostedSimilarity → Similarity → UpdatedAt
    ↓
[8] Select TopK
    ├─ Take top 5-10 documents
    └─ Log RefTypes breakdown
    ↓
[9] Build Prompt
    ├─ System Prompt (hướng dẫn AI)
    ├─ User Payload (question + documents + instructions)
    └─ Serialize to JSON
    ↓
[10] Call LLM
    ├─ Gemini Chat API (gemini-2.5-flash)
    └─ Get JSON response
    ↓
[11] Extract Answer
    ├─ Parse JSON response
    ├─ Extract answer text
    └─ Parse metadata
    ↓
[12] Return Response
    ├─ Answer text
    ├─ Documents (nếu includeDebugDocuments)
    └─ Metadata (refTypes, threshold, etc.)
```

## Các tính năng đặc biệt

### 1. **Auto-suggest RefTypes**

Hệ thống tự động detect keywords trong query và suggest RefTypes phù hợp:

```csharp
"đơn hàng" → ["order", "order_line"]
"hóa đơn" → ["invoice", "order"]
"khách hàng" → ["customer", "order"]
"sách" → ["order_line", "book"]
```

### 2. **Adaptive Threshold**

Không dùng threshold cố định, mà tính động dựa trên:
- Distribution của similarities
- Số lượng documents có similarity cao
- Percentile (p75, p50) và mean

**Ví dụ:**
- Query dễ match (nhiều documents similarity >0.7) → threshold cao (0.5-0.65)
- Query khó match (ít documents similarity >0.7) → threshold thấp (0.3-0.4)

### 3. **RefType Boosting**

Boost documents có RefType phù hợp với query để đảm bảo kết quả chính xác.

**Ví dụ:**
- Query "hóa đơn" → boost invoice documents (+0.1 điểm)
- Query "khách hàng" → boost customer documents (+0.1 điểm)

### 4. **Dynamic TopK**

Tự động tăng topK nếu có nhiều documents match:
- >20 matches và topK < 10 → tăng lên 10
- >10 matches và topK < 8 → tăng lên 8

**Lý do:** Đảm bảo AI có đủ context để trả lời chính xác.

### 5. **Metadata trong Response**

Response bao gồm metadata để debug:
- `requestedRefTypes`: RefTypes được yêu cầu
- `availableRefTypes`: RefTypes có trong DB
- `missingRefTypes`: RefTypes thiếu dữ liệu
- `similarityThreshold`: Threshold được tính
- `documentsCount`: Số documents được chọn

## Performance Considerations

### 1. **Embedding Generation**
- Rate limiting: Delay 200ms giữa các requests
- Batch save: Mỗi 20 documents
- Async processing

### 2. **Similarity Calculation**
- In-memory calculation (không query DB nhiều lần)
- Cosine similarity: O(n) với n = vector dimension (768)
- Parallel processing có thể cải thiện

### 3. **Database Queries**
- Sử dụng `AsNoTracking()` để tăng performance
- Index trên `ref_type` và `ref_id`
- Load tất cả documents một lần, filter in-memory

## Limitations & Future Improvements

### Limitations hiện tại:
1. Chỉ hỗ trợ 5 RefTypes (order, order_line, invoice, book, customer)
2. Embedding model: text-embedding-004 (768 dimensions)
3. LLM: gemini-2.5-flash (có thể upgrade lên pro)
4. Không có caching cho embeddings
5. Không có re-ranking với cross-encoder
6. Similarity calculation là single-threaded

### Future Improvements:
1. **Hybrid Search**: Kết hợp vector search + keyword search (BM25)
2. **Re-ranking**: Dùng cross-encoder để re-rank top documents
3. **Caching**: Cache query embeddings và results
4. **Multi-modal**: Hỗ trợ search bằng hình ảnh
5. **Conversation Context**: Giữ context của cuộc hội thoại
6. **Feedback Loop**: Học từ user feedback để cải thiện
7. **Parallel Processing**: Tính similarity song song
8. **Vector Database**: Dùng dedicated vector DB (Pinecone, Weaviate) thay vì MySQL

## API Endpoints

### 1. Search Knowledge Base
```
POST /api/ai/search
Body: {
  "query": "khách hàng nào mua nhiều nhất",
  "topK": 5,
  "refTypes": null,  // null = auto-suggest
  "language": "vi",
  "includeDebugDocuments": true
}

Response: {
  "success": true,
  "data": {
    "answer": "...",
    "documents": [...],  // nếu includeDebugDocuments = true
    "metadata": {
      "requestedRefTypes": ["customer", "order"],
      "availableRefTypes": ["customer", "order", "book"],
      "similarityThreshold": 0.45,
      "documentsCount": 5
    }
  }
}
```

### 2. Rebuild Index
```
POST /api/ai/search/reindex
Body: {
  "refTypes": ["order", "order_line", "invoice", "book", "customer"],
  "truncateBeforeInsert": true,
  "maxBooks": 1000,
  "maxCustomers": 500,
  "maxOrders": 1000,
  "historyDays": 180
}

Response: {
  "success": true,
  "data": {
    "indexedDocuments": 2500,
    "indexedAt": "2025-01-22T10:30:00Z",
    "refTypes": ["order", "order_line", "invoice", "book", "customer"]
  }
}
```

## Best Practices

### 1. **Reindex thường xuyên**
- Reindex khi có dữ liệu mới
- Reindex khi cấu trúc dữ liệu thay đổi
- Có thể schedule reindex hàng ngày/tuần

### 2. **Query Optimization**
- Query càng cụ thể càng tốt
- Sử dụng RefTypes phù hợp khi biết rõ loại dữ liệu cần
- Bật `includeDebugDocuments` để debug

### 3. **Monitoring**
- Log similarity scores để theo dõi chất lượng
- Monitor threshold values
- Track RefTypes được chọn
- Monitor response time

## Troubleshooting

### Vấn đề: Không tìm thấy documents
**Nguyên nhân:**
- Threshold quá cao
- Chưa reindex với RefTypes phù hợp
- Query không khớp với content

**Giải pháp:**
- Kiểm tra metadata trong response
- Giảm threshold hoặc reindex
- Cải thiện query

### Vấn đề: Kết quả không chính xác
**Nguyên nhân:**
- Documents không phù hợp được chọn
- Prompt không đủ rõ ràng
- Thiếu context

**Giải pháp:**
- Tăng topK
- Cải thiện prompt
- Thêm RefTypes liên quan

### Vấn đề: Response chậm
**Nguyên nhân:**
- Quá nhiều documents được tính similarity
- LLM response chậm
- Network latency

**Giải pháp:**
- Giảm số documents trong DB
- Tăng threshold
- Sử dụng model nhanh hơn

## Code Examples

### Tính Cosine Similarity
```csharp
var similarity = CosineSimilarity(queryEmbedding, documentEmbedding);
// similarity: 0.0 - 1.0
// >0.7: rất liên quan
// 0.3-0.7: liên quan vừa phải
// <0.3: ít liên quan
```

### Adaptive Threshold
```csharp
var threshold = CalculateAdaptiveThreshold(allSimilarities);
// threshold: 0.3 - 0.65 (tùy distribution)
// Tự động điều chỉnh dựa trên chất lượng documents
```

### Boost RefTypes
```csharp
var prioritizedRefTypes = GetPrioritizedRefTypesForQuery(query);
// prioritizedRefTypes: ["order", "order_line"] cho query "đơn hàng"
// Boost score: +0.1 điểm similarity
```

## References

- **Gemini Embedding API**: https://ai.google.dev/docs/embeddings
- **Gemini Chat API**: https://ai.google.dev/docs/gemini_api_overview
- **Vector Similarity Search**: https://en.wikipedia.org/wiki/Cosine_similarity
- **RAG (Retrieval-Augmented Generation)**: https://arxiv.org/abs/2005.11401
- **Semantic Search**: https://www.pinecone.io/learn/semantic-search/
