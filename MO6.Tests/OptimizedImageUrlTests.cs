using MyProject12.Services;
using Xunit;

public class OptimizedImageUrlTests
{
    [Fact]
    public void ApplyWebpDefaults_JpegCrop_AppendsCanonicalWebpOptions()
    {
        var result = OptimizedImageUrl.ApplyWebpDefaults("/media/image.jpg?width=540&height=380");

        Assert.Equal("/media/image.jpg?width=540&height=380&format=webp&quality=80", result);
    }

    [Fact]
    public void ApplyWebpDefaults_ExistingFormatAndQuality_ReplacesWithCanonicalOptions()
    {
        var result = OptimizedImageUrl.ApplyWebpDefaults("/media/image.jpeg?width=540&quality=60&format=jpg&height=380");

        Assert.Equal("/media/image.jpeg?width=540&height=380&format=webp&quality=80", result);
    }

    [Fact]
    public void ApplyWebpDefaults_WebpSource_DoesNotForceQuality()
    {
        var result = OptimizedImageUrl.ApplyWebpDefaults("/media/image.webp?width=540&height=380");

        Assert.Equal("/media/image.webp?width=540&height=380", result);
    }

    [Fact]
    public void ApplyWebpDefaults_WebpSourceWithLowerQuality_KeepsExistingQuality()
    {
        var result = OptimizedImageUrl.ApplyWebpDefaults("/media/image.webp?width=540&quality=60");

        Assert.Equal("/media/image.webp?width=540&quality=60", result);
    }

    [Fact]
    public void ApplyWebpDefaults_SvgSource_DoesNotAppendImageSharpOptions()
    {
        var result = OptimizedImageUrl.ApplyWebpDefaults("/media/logo.svg");

        Assert.Equal("/media/logo.svg", result);
    }
}
