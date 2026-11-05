using UseCase.Fashion;

namespace UseCase.Tests.Fashion;

public class OrderRulesTest
{
    [Theory]
    [InlineData("Pending", "Confirmed", false, true)]
    [InlineData("Pending", "Cancelled", false, true)]
    [InlineData("Confirmed", "Shipping", false, true)]
    [InlineData("Shipping", "Completed", false, true)]
    [InlineData("Shipping", "Cancelled", false, false)]
    [InlineData("Shipping", "Cancelled", true, true)]
    [InlineData("Completed", "Cancelled", true, false)]
    [InlineData("Cancelled", "Pending", false, false)]
    public void OrderTransitionsRespectLifecycle(string current, string next, bool returned, bool expected)
        => Assert.Equal(expected, FashionRules.CanTransition(current, next, returned));
}
