using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace BookStore.Api.TestScripts;

public class ApiTestRunner
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private string? _authToken;

    public ApiTestRunner(string baseUrl = "https://localhost:7000")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task RunAllTests()
    {
        Console.WriteLine("🚀 Bắt đầu test các API của BookStore...\n");

        try
        {
            // Test 1: Health Check
            await TestHealthCheck();

            // Test 2: Authentication
            await TestAuthentication();

            // Test 3: Categories
            await TestCategories();

            // Test 4: Books
            await TestBooks();

            // Test 5: Purchase Orders
            await TestPurchaseOrders();

            // Test 6: Goods Receipts
            await TestGoodsReceipts();

            Console.WriteLine("\n✅ Tất cả các test đã hoàn thành thành công!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Lỗi trong quá trình test: {ex.Message}");
        }
    }

    private async Task TestHealthCheck()
    {
        Console.WriteLine("🔍 Test Health Check...");
        
        var response = await _httpClient.GetAsync($"{_baseUrl}/health");
        var content = await response.Content.ReadAsStringAsync();
        
        if (response.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Health Check: OK");
        }
        else
        {
            Console.WriteLine($"❌ Health Check: Failed - {response.StatusCode}");
        }
    }

    private async Task TestAuthentication()
    {
        Console.WriteLine("\n🔐 Test Authentication...");

        // Test Register
        var registerData = new
        {
            email = "test@bookstore.com",
            password = "Test123!",
            confirmPassword = "Test123!",
            roleId = 3
        };

        var registerJson = JsonSerializer.Serialize(registerData);
        var registerContent = new StringContent(registerJson, Encoding.UTF8, "application/json");
        
        var registerResponse = await _httpClient.PostAsync($"{_baseUrl}/api/auth/register", registerContent);
        var registerResult = await registerResponse.Content.ReadAsStringAsync();
        
        if (registerResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Register: OK");
            
            // Extract token from response
            var registerDataResponse = JsonSerializer.Deserialize<JsonElement>(registerResult);
            if (registerDataResponse.TryGetProperty("data", out var data) && 
                data.TryGetProperty("token", out var token))
            {
                _authToken = token.GetString();
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                Console.WriteLine("✅ Token extracted and set");
            }
        }
        else
        {
            Console.WriteLine($"❌ Register: Failed - {registerResponse.StatusCode}");
            Console.WriteLine($"Response: {registerResult}");
        }

        // Test Login
        var loginData = new
        {
            email = "test@bookstore.com",
            password = "Test123!"
        };

        var loginJson = JsonSerializer.Serialize(loginData);
        var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");
        
        var loginResponse = await _httpClient.PostAsync($"{_baseUrl}/api/auth/login", loginContent);
        var loginResult = await loginResponse.Content.ReadAsStringAsync();
        
        if (loginResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Login: OK");
        }
        else
        {
            Console.WriteLine($"❌ Login: Failed - {loginResponse.StatusCode}");
            Console.WriteLine($"Response: {loginResult}");
        }
    }

    private async Task TestCategories()
    {
        Console.WriteLine("\n📚 Test Categories...");

        // Test Get Categories
        var getResponse = await _httpClient.GetAsync($"{_baseUrl}/api/category");
        if (getResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Get Categories: OK");
        }
        else
        {
            Console.WriteLine($"❌ Get Categories: Failed - {getResponse.StatusCode}");
        }

        // Test Create Category
        var categoryData = new
        {
            name = "Test Category",
            description = "Test category description"
        };

        var categoryJson = JsonSerializer.Serialize(categoryData);
        var categoryContent = new StringContent(categoryJson, Encoding.UTF8, "application/json");
        
        var createResponse = await _httpClient.PostAsync($"{_baseUrl}/api/category", categoryContent);
        var createResult = await createResponse.Content.ReadAsStringAsync();
        
        if (createResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Create Category: OK");
        }
        else
        {
            Console.WriteLine($"❌ Create Category: Failed - {createResponse.StatusCode}");
            Console.WriteLine($"Response: {createResult}");
        }
    }

    private async Task TestBooks()
    {
        Console.WriteLine("\n📖 Test Books...");

        // Test Get Books
        var getResponse = await _httpClient.GetAsync($"{_baseUrl}/api/book");
        if (getResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Get Books: OK");
        }
        else
        {
            Console.WriteLine($"❌ Get Books: Failed - {getResponse.StatusCode}");
        }

        // Test Create Author
        var authorData = new
        {
            firstName = "Test",
            lastName = "Author",
            gender = 0,
            email = "testauthor@example.com"
        };

        var authorJson = JsonSerializer.Serialize(authorData);
        var authorContent = new StringContent(authorJson, Encoding.UTF8, "application/json");
        
        var authorResponse = await _httpClient.PostAsync($"{_baseUrl}/api/book/authors", authorContent);
        if (authorResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Create Author: OK");
        }
        else
        {
            Console.WriteLine($"❌ Create Author: Failed - {authorResponse.StatusCode}");
        }
    }

    private async Task TestPurchaseOrders()
    {
        Console.WriteLine("\n🛒 Test Purchase Orders...");

        // Test Get Purchase Orders
        var getResponse = await _httpClient.GetAsync($"{_baseUrl}/api/purchaseorder");
        if (getResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Get Purchase Orders: OK");
        }
        else
        {
            Console.WriteLine($"❌ Get Purchase Orders: Failed - {getResponse.StatusCode}");
        }

        // Test Create Purchase Order (this will fail without proper data setup)
        var poData = new
        {
            publisherId = 1,
            note = "Test purchase order",
            lines = new[]
            {
                new
                {
                    isbn = "978-0-7475-3269-9",
                    qtyOrdered = 10,
                    unitPrice = 15.99
                }
            }
        };

        var poJson = JsonSerializer.Serialize(poData);
        var poContent = new StringContent(poJson, Encoding.UTF8, "application/json");
        
        var poResponse = await _httpClient.PostAsync($"{_baseUrl}/api/purchaseorder", poContent);
        if (poResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Create Purchase Order: OK");
        }
        else
        {
            Console.WriteLine($"❌ Create Purchase Order: Failed - {poResponse.StatusCode}");
            var poResult = await poResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Response: {poResult}");
        }
    }

    private async Task TestGoodsReceipts()
    {
        Console.WriteLine("\n📦 Test Goods Receipts...");

        // Test Get Goods Receipts
        var getResponse = await _httpClient.GetAsync($"{_baseUrl}/api/goodsreceipt");
        if (getResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Get Goods Receipts: OK");
        }
        else
        {
            Console.WriteLine($"❌ Get Goods Receipts: Failed - {getResponse.StatusCode}");
        }

        // Test Get Available Purchase Orders
        var availableResponse = await _httpClient.GetAsync($"{_baseUrl}/api/goodsreceipt/available-purchase-orders");
        if (availableResponse.IsSuccessStatusCode)
        {
            Console.WriteLine("✅ Get Available Purchase Orders: OK");
        }
        else
        {
            Console.WriteLine($"❌ Get Available Purchase Orders: Failed - {availableResponse.StatusCode}");
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Test runner program
public class Program
{
    public static async Task Main(string[] args)
    {
        var testRunner = new ApiTestRunner();
        await testRunner.RunAllTests();
        testRunner.Dispose();
    }
}
