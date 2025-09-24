using Amazon.S3;
using Amazon.S3.Model;
using FluentAssertions;
using Moq;
using Xunit;

namespace SVEDB_Extract.Tests;

public class ClientTests
{
    private readonly Mock<IAmazonS3> _s3Mock;

    public ClientTests()
    {
        _s3Mock = new Mock<IAmazonS3>();
    }

    [Fact]
    public async Task GetExistingCardsFromBucket_Should_ReturnListWithoutPNGExtension()
    {
        ListObjectsV2Response response = new()
        {
            S3Objects =
            [
                new S3Object
                {
                    Key = "BP01-001EN.png"
                },
                new S3Object
                {
                    Key = "BP01-002EN.png"
                },
                new S3Object
                {
                    Key = "BP01-003EN.png"
                }
            ]
        };

        _s3Mock.Setup(mock => mock.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        Client client = new(true, _s3Mock.Object);
        var keys = await client.GetExistingCardsFromBucket();

        keys.Should().NotBeNull();
        keys.Should().HaveCount(response.S3Objects.Count);
        keys.Should().NotContain(".png");
    }

    [Fact]
    public async Task GetExistingCardsFromBucket_Should_ReturnEmptyArrayWhenBucketIsEmpty()
    {
        ListObjectsV2Response response = new()
        {
            S3Objects = new()
            {
            }
        };

        _s3Mock.Setup(mock => mock.ListObjectsV2Async(It.IsAny<ListObjectsV2Request>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        Client client = new(true, _s3Mock.Object);
        var keys = await client.GetExistingCardsFromBucket();

        keys.Should().NotBeNull();
        keys.Should().BeEmpty();
    }
}