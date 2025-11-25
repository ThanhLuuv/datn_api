# Tài liệu Nguyên lý và Ứng dụng AI Search trong BookStore.Api

Tài liệu này giải thích cơ chế hoạt động, nguyên lý và cách triển khai tính năng AI Search trong dự án BookStore.Api.

## 1. Tổng quan

AI Search trong dự án này là một hệ thống **RAG (Retrieval-Augmented Generation)**. Thay vì chỉ tìm kiếm theo từ khóa chính xác (keyword matching) như SQL `LIKE`, hệ thống sử dụng **Semantic Search** (tìm kiếm theo ngữ nghĩa) kết hợp với **Generative AI** (Gemini) để trả lời câu hỏi của người dùng dựa trên dữ liệu thực tế của hệ thống.

### Mục tiêu
- Cho phép người dùng hỏi bằng ngôn ngữ tự nhiên (ví dụ: "Doanh thu sách IT tháng này thế nào?", "Đơn hàng của khách Nguyễn Văn A đã giao chưa?").
- Tìm kiếm thông tin chính xác từ cơ sở dữ liệu (đơn hàng, sách, khách hàng, hóa đơn).
- Tổng hợp thông tin và trả lời một cách tự nhiên, có trích dẫn dữ liệu.

## 2. Kiến trúc hệ thống

Hệ thống bao gồm các thành phần chính:

1.  **Database (`AiDocuments` table)**: Lưu trữ dữ liệu đã được "vector hóa".
    -   `RefType`, `RefId`: Tham chiếu đến dữ liệu gốc (ví dụ: `order`, `123`).
    -   `Content`: Nội dung văn bản tóm tắt của dữ liệu (dùng để gửi cho AI).
    -   `EmbeddingJson`: Vector số (embedding) biểu diễn ngữ nghĩa của `Content`.

2.  **AI Search Service (`AiSearchService.cs`)**: Trung tâm xử lý logic.
    -   Quản lý Indexing (đồng bộ dữ liệu từ DB sang `AiDocuments`).
    -   Xử lý Search (tìm kiếm vector, ranking, prompt engineering).

3.  **LLM Client (`GeminiClient.cs`)**: Giao tiếp với Google Gemini.
    -   `GetEmbeddingAsync`: Chuyển văn bản thành vector.
    -   `CallGeminiAsync`: Gửi prompt và context để sinh câu trả lời.

## 3. Quy trình hoạt động (Workflow)

### A. Quy trình Indexing (Đánh chỉ mục)
Để AI có thể tìm kiếm, dữ liệu cần được chuẩn bị trước. Quy trình này nằm trong hàm `RebuildAiSearchIndexAsync`.

1.  **Thu thập dữ liệu**: Lấy dữ liệu từ các bảng `Books`, `Orders`, `Customers`, `Invoices`, v.v.
2.  **Tạo Seed Content**: Chuyển đổi dữ liệu cấu trúc (row/object) thành văn bản bán cấu trúc (text).
    -   *Ví dụ*: Một đơn hàng sẽ được chuyển thành text chứa OrderId, tên khách, danh sách sản phẩm, tổng tiền...
3.  **Vector hóa (Embedding)**: Gửi văn bản này lên Gemini để lấy về một vector (mảng số thực, ví dụ: `[0.1, -0.5, 0.8, ...]`). Vector này đại diện cho ý nghĩa của văn bản.
4.  **Lưu trữ**: Lưu `Content` và `Embedding` vào bảng `AiDocuments`.

### B. Quy trình Search (Tìm kiếm)
Khi người dùng đặt câu hỏi, quy trình xử lý trong `SearchKnowledgeBaseAsync` diễn ra như sau:

1.  **Phân tích câu hỏi**:
    -   Hệ thống tự động đoán loại dữ liệu (`RefTypes`) người dùng quan tâm dựa trên từ khóa (ví dụ: "hóa đơn" -> tìm trong `invoice` và `order`).
    -   Hàm: `SuggestRefTypesFromQuery`.

2.  **Vector hóa câu hỏi**:
    -   Câu hỏi của người dùng cũng được chuyển thành vector (Query Embedding) dùng cùng model với lúc Indexing.

3.  **Tìm kiếm tương đồng (Similarity Search)**:
    -   Tính khoảng cách (Cosine Similarity) giữa vector câu hỏi và vector của tất cả tài liệu trong DB.
    -   Điểm càng cao (gần 1) nghĩa là tài liệu càng liên quan đến câu hỏi.

4.  **Lọc và Xếp hạng (Filtering & Ranking)**:
    -   **Adaptive Threshold**: Loại bỏ các tài liệu có điểm tương đồng thấp. Ngưỡng này được tính động dựa trên phân phối điểm số của kết quả tìm được.
    -   **Boosting**: Tăng điểm cho các tài liệu quan trọng (ví dụ: nếu hỏi về "đơn hàng", tài liệu loại `order` sẽ được cộng điểm).
    -   **Recency**: Ưu tiên dữ liệu mới hơn (dựa trên `UpdatedAt`).

5.  **Sinh câu trả lời (Generation)**:
    -   Lấy Top K tài liệu tốt nhất (Context).
    -   Ghép câu hỏi + Context + System Prompt (hướng dẫn cách trả lời).
    -   Gửi toàn bộ cho Gemini để sinh câu trả lời cuối cùng.

## 4. Các khái niệm cốt lõi

### Embedding (Vector)
Là cách biểu diễn văn bản dưới dạng số học để máy tính có thể "hiểu" ngữ nghĩa.
-   *Ví dụ*: Vector của từ "Vua" trừ đi "Nam" cộng "Nữ" sẽ ra vector gần với "Nữ hoàng".
-   Trong dự án này, chúng ta dùng Embedding để so sánh sự tương đồng về ý nghĩa giữa câu hỏi và dữ liệu, thay vì so khớp từ khóa chính xác.

### Cosine Similarity
Là công thức toán học để đo độ tương đồng giữa 2 vector.
-   Giá trị từ -1 đến 1.
-   Trong code: Hàm `CosineSimilarity` tính tích vô hướng chia cho tích độ dài.

### RAG (Retrieval-Augmented Generation)
Kỹ thuật kết hợp sức mạnh tìm kiếm (Retrieval) và khả năng ngôn ngữ của AI (Generation).
-   AI không "nhớ" dữ liệu của bạn trong đầu nó.
-   Chúng ta phải "mớm" (retrieve) dữ liệu liên quan cho nó mỗi khi hỏi.

## 5. Hướng dẫn đọc code (Code Map)

Để hiểu sâu hơn, bạn hãy đọc các file sau theo thứ tự:

1.  **`Services/AiSearchService.cs`**:
    -   `RebuildAiSearchIndexAsync`: Logic tạo dữ liệu cho AI. Chú ý cách dùng `StringBuilder` để tạo nội dung text cho từng loại dữ liệu (`book`, `order`...).
    -   `SearchKnowledgeBaseAsync`: Logic tìm kiếm chính.
    -   `CosineSimilarity`: Hàm tính toán cốt lõi.
    -   `CalculateAdaptiveThreshold`: Logic thông minh để lọc kết quả nhiễu.

2.  **`Services/GeminiClient.cs`**:
    -   Xem cách gọi API Gemini để lấy Embedding và Chat Completion.

3.  **`Models/AiDocument.cs`** (hoặc tương đương trong thư mục Models/Data):
    -   Xem cấu trúc bảng lưu trữ vector.

## 6. Ví dụ thực tế

**Câu hỏi**: "Đơn hàng của anh Nam hôm qua bao nhiêu tiền?"

1.  **Phân tích**: Từ khóa "đơn hàng", "Nam". -> Hệ thống khoanh vùng tìm trong `order`, `customer`.
2.  **Embedding**: Chuyển câu hỏi thành vector `V_q`.
3.  **Scan**: So sánh `V_q` với vector của các đơn hàng trong DB.
4.  **Match**: Tìm thấy văn bản đơn hàng có chứa "Khách hàng: Trần Văn Nam... Ngày đặt: [hôm qua]... Tổng tiền: 500k". Vector của văn bản này sẽ rất gần `V_q`.
5.  **Prompt**: Gửi cho AI:
    -   *Context*: "Đơn hàng #123, Khách: Trần Văn Nam, Ngày: 24/11, Tổng: 500.000 VNĐ..."
    -   *Question*: "Đơn hàng của anh Nam hôm qua bao nhiêu tiền?"
6.  **Answer**: AI trả lời: "Đơn hàng của khách hàng Trần Văn Nam đặt hôm qua có tổng trị giá là 500.000 VNĐ."
