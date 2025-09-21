using BookStore.Api.Data;
using BookStore.Api.DTOs;
using BookStore.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BookStore.Api.Services;

public class PublisherService : IPublisherService
{
    private readonly BookStoreDbContext _context;

    public PublisherService(BookStoreDbContext context)
    {
        _context = context;
    }

    public async Task<ApiResponse<PublisherListResponse>> GetPublishersAsync(int pageNumber, int pageSize, string? searchTerm = null)
    {
        try
        {
            var query = _context.Publishers.AsQueryable();

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Name.Contains(searchTerm) || 
                                       p.Address.Contains(searchTerm) || 
                                       p.Email.Contains(searchTerm));
            }

            // Get total count
            var totalCount = await query.CountAsync();

            // Apply pagination
            var publishers = await query
                .OrderBy(p => p.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PublisherDto
                {
                    PublisherId = p.PublisherId,
                    Name = p.Name,
                    Address = p.Address,
                    Email = p.Email,
                    Phone = p.Phone,
                    BookCount = _context.Books.Count(b => b.PublisherId == p.PublisherId)
                })
                .ToListAsync();

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var response = new PublisherListResponse
            {
                Publishers = publishers,
                TotalCount = totalCount,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return new ApiResponse<PublisherListResponse>
            {
                Success = true,
                Message = "Lấy danh sách nhà xuất bản thành công",
                Data = response
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PublisherListResponse>
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy danh sách nhà xuất bản",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PublisherDto>> GetPublisherByIdAsync(long publisherId)
    {
        try
        {
            var publisher = await _context.Publishers
                .Where(p => p.PublisherId == publisherId)
                .Select(p => new PublisherDto
                {
                    PublisherId = p.PublisherId,
                    Name = p.Name,
                    Address = p.Address,
                    Email = p.Email,
                    Phone = p.Phone,
                    BookCount = _context.Books.Count(b => b.PublisherId == p.PublisherId)
                })
                .FirstOrDefaultAsync();

            if (publisher == null)
            {
                return new ApiResponse<PublisherDto>
                {
                    Success = false,
                    Message = "Không tìm thấy nhà xuất bản",
                    Errors = new List<string> { "Nhà xuất bản không tồn tại" }
                };
            }

            return new ApiResponse<PublisherDto>
            {
                Success = true,
                Message = "Lấy thông tin nhà xuất bản thành công",
                Data = publisher
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PublisherDto>
            {
                Success = false,
                Message = "Có lỗi xảy ra khi lấy thông tin nhà xuất bản",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PublisherDto>> CreatePublisherAsync(CreatePublisherDto createPublisherDto)
    {
        try
        {
            // Check if publisher with same name already exists
            var existingPublisher = await _context.Publishers
                .FirstOrDefaultAsync(p => p.Name == createPublisherDto.Name);

            if (existingPublisher != null)
            {
                return new ApiResponse<PublisherDto>
                {
                    Success = false,
                    Message = "Nhà xuất bản đã tồn tại",
                    Errors = new List<string> { "Tên nhà xuất bản đã được sử dụng" }
                };
            }

            // Check if email already exists
            var existingEmail = await _context.Publishers
                .FirstOrDefaultAsync(p => p.Email == createPublisherDto.Email);

            if (existingEmail != null)
            {
                return new ApiResponse<PublisherDto>
                {
                    Success = false,
                    Message = "Email đã tồn tại",
                    Errors = new List<string> { "Email đã được sử dụng" }
                };
            }

            var publisher = new Publisher
            {
                Name = createPublisherDto.Name,
                Address = createPublisherDto.Address,
                Email = createPublisherDto.Email,
                Phone = createPublisherDto.Phone
            };

            _context.Publishers.Add(publisher);
            await _context.SaveChangesAsync();

            var publisherDto = new PublisherDto
            {
                PublisherId = publisher.PublisherId,
                Name = publisher.Name,
                Address = publisher.Address,
                Email = publisher.Email,
                Phone = publisher.Phone,
                BookCount = 0
            };

            return new ApiResponse<PublisherDto>
            {
                Success = true,
                Message = "Tạo nhà xuất bản thành công",
                Data = publisherDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PublisherDto>
            {
                Success = false,
                Message = "Có lỗi xảy ra khi tạo nhà xuất bản",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<PublisherDto>> UpdatePublisherAsync(long publisherId, UpdatePublisherDto updatePublisherDto)
    {
        try
        {
            var publisher = await _context.Publishers
                .FirstOrDefaultAsync(p => p.PublisherId == publisherId);

            if (publisher == null)
            {
                return new ApiResponse<PublisherDto>
                {
                    Success = false,
                    Message = "Không tìm thấy nhà xuất bản",
                    Errors = new List<string> { "Nhà xuất bản không tồn tại" }
                };
            }

            // Check if another publisher with same name exists
            var existingPublisher = await _context.Publishers
                .FirstOrDefaultAsync(p => p.Name == updatePublisherDto.Name && p.PublisherId != publisherId);

            if (existingPublisher != null)
            {
                return new ApiResponse<PublisherDto>
                {
                    Success = false,
                    Message = "Tên nhà xuất bản đã tồn tại",
                    Errors = new List<string> { "Tên nhà xuất bản đã được sử dụng" }
                };
            }

            // Check if another publisher with same email exists
            var existingEmail = await _context.Publishers
                .FirstOrDefaultAsync(p => p.Email == updatePublisherDto.Email && p.PublisherId != publisherId);

            if (existingEmail != null)
            {
                return new ApiResponse<PublisherDto>
                {
                    Success = false,
                    Message = "Email đã tồn tại",
                    Errors = new List<string> { "Email đã được sử dụng" }
                };
            }

            publisher.Name = updatePublisherDto.Name;
            publisher.Address = updatePublisherDto.Address;
            publisher.Email = updatePublisherDto.Email;
            publisher.Phone = updatePublisherDto.Phone;

            await _context.SaveChangesAsync();

            var publisherDto = new PublisherDto
            {
                PublisherId = publisher.PublisherId,
                Name = publisher.Name,
                Address = publisher.Address,
                Email = publisher.Email,
                Phone = publisher.Phone,
                BookCount = await _context.Books.CountAsync(b => b.PublisherId == publisher.PublisherId)
            };

            return new ApiResponse<PublisherDto>
            {
                Success = true,
                Message = "Cập nhật nhà xuất bản thành công",
                Data = publisherDto
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<PublisherDto>
            {
                Success = false,
                Message = "Có lỗi xảy ra khi cập nhật nhà xuất bản",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ApiResponse<bool>> DeletePublisherAsync(long publisherId)
    {
        try
        {
            var publisher = await _context.Publishers
                .FirstOrDefaultAsync(p => p.PublisherId == publisherId);

            if (publisher == null)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không tìm thấy nhà xuất bản",
                    Errors = new List<string> { "Nhà xuất bản không tồn tại" }
                };
            }

            // Check if publisher has books
            var hasBooks = await _context.Books.AnyAsync(b => b.PublisherId == publisherId);
            if (hasBooks)
            {
                return new ApiResponse<bool>
                {
                    Success = false,
                    Message = "Không thể xóa nhà xuất bản có sách",
                    Errors = new List<string> { "Nhà xuất bản đang có sách, không thể xóa" }
                };
            }

            _context.Publishers.Remove(publisher);
            await _context.SaveChangesAsync();

            return new ApiResponse<bool>
            {
                Success = true,
                Message = "Xóa nhà xuất bản thành công",
                Data = true
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse<bool>
            {
                Success = false,
                Message = "Có lỗi xảy ra khi xóa nhà xuất bản",
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
