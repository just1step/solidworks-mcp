namespace SolidWorksBridge.Tests;

public class ProgramTests
{
    [Fact]
    public void AppName_ShouldBeCorrect()
    {
        Assert.Equal("SolidWorksBridge", Program.AppName);
    }

    [Fact]
    public void PipeName_ShouldBeCorrect()
    {
        Assert.Equal("SolidWorksMcpBridge", Program.PipeName);
    }
}