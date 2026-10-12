using Checkout.Application.Abstractions.Caching;
using Checkout.Application.Abstractions.Persistence;
using Checkout.Application.Common;
using Checkout.Application.Customers;
using Checkout.Application.Customers.DTOs;
using Checkout.Domain.Entities;
using Moq;

namespace Checkout.Application.Tests.Customers;

public sealed class CustomerServiceTests
{
    private static readonly CustomerDto WeiLing = new(1, "Wei Ling", "Tan", "weiling.tan@example.com");

    private readonly Mock<ICustomerRepository> _customers = new();
    private readonly Mock<ICustomerCardRepository> _cards = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly CustomerService _sut;

    public CustomerServiceTests()
    {
        _customers.Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new Customer(1, "Wei Ling", "Tan", "weiling.tan@example.com")]);

        _sut = new CustomerService(_customers.Object, _cards.Object, _cache.Object);
    }

    [Fact]
    public async Task GetAllAsync_OnACacheHit_DoesNotQueryTheDatabase()
    {
        _cache.Setup(c => c.GetAsync<IReadOnlyList<CustomerDto>>(CacheKeys.Customers)).ReturnsAsync([WeiLing]);

        var result = await _sut.GetAllAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Equal(WeiLing);
        _customers.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.SetAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CustomerDto>>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_OnACacheMiss_LoadsFromTheRepositoryAndCachesForTenMinutes()
    {
        var result = await _sut.GetAllAsync();

        result.Value.Should().Equal(WeiLing);
        _customers.Verify(r => r.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(
            c => c.SetAsync(
                CacheKeys.Customers,
                It.Is<IReadOnlyList<CustomerDto>>(list => list.SequenceEqual(new[] { WeiLing })),
                TimeSpan.FromMinutes(10)),
            Times.Once);
    }

    [Fact]
    public async Task GetSavedCardsAsync_ForAnUnknownCustomer_ReturnsCustomerNotFound()
    {
        var result = await _sut.GetSavedCardsAsync(99);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(ErrorCodes.CustomerNotFound);
        _cards.Verify(r => r.GetByCustomerAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetSavedCardsAsync_OnACacheMiss_LoadsTheCustomersCardsAndCachesThem()
    {
        var savedAt = new DateTime(2026, 10, 7, 9, 30, 0, DateTimeKind.Utc);
        _cards.Setup(r => r.GetByCustomerAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CustomerCard(1, "token", "411111XXXXXX1111", savedAt)]);

        var result = await _sut.GetSavedCardsAsync(1);

        result.Value.Should().ContainSingle().Which.MaskedCardNo.Should().Be("411111XXXXXX1111");
        _cache.Verify(
            c => c.SetAsync(CacheKeys.CustomerCards(1), It.IsAny<IReadOnlyList<SavedCardDto>>(), CacheKeys.CardsTtl),
            Times.Once);
    }
}
