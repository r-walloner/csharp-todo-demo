using TodoDemo.Contracts;

namespace TodoDemo.Tests.Contracts;

// AI-GENERATED: this test file was written by an AI coding assistant. Review before
// relying on it as a spec of intended behavior.
public class PagedResponseTests
{
    [Theory]
    [InlineData(0, 10, 0)]
    [InlineData(25, 10, 3)]
    [InlineData(20, 10, 2)]
    [InlineData(1, 10, 1)]
    public void TotalPages_ComputesCeilingOfTotalCountOverPageSize(int totalCount, int pageSize, int expectedTotalPages)
    {
        var response = new PagedResponse<int>([], Page: 1, PageSize: pageSize, TotalCount: totalCount);

        Assert.Equal(expectedTotalPages, response.TotalPages);
    }

    [Fact]
    public void TotalPages_PageSizeZero_ReturnsZeroInsteadOfDividingByZero()
    {
        // Neither controller can actually produce PageSize == 0 today (both clamp it to
        // at least 1), but PagedResponse<T> is public API, so this guard clause deserves
        // its own test regardless of whether current callers can reach it.
        var response = new PagedResponse<int>([], Page: 1, PageSize: 0, TotalCount: 10);

        Assert.Equal(0, response.TotalPages);
    }
}
