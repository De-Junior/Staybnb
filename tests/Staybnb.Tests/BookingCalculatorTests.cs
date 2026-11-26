using Microsoft.Extensions.Options;
using Staybnb.Services;

namespace Staybnb.Tests;

public class BookingCalculatorTests
{
    private static BookingCalculator Calculator(decimal? serviceFeeRate = null) =>
        new(Options.Create(serviceFeeRate is null ? new BookingOptions() : new BookingOptions { ServiceFeeRate = serviceFeeRate.Value }));

    [Fact]
    public void Total_is_nights_subtotal_plus_cleaning_fee_plus_service_fee()
    {
        // 3 nights x R1000 = R3000; service fee 12% = R360; cleaning R200
        var quote = Calculator().Quote(1000m, 200m, new DateTime(2026, 12, 1), new DateTime(2026, 12, 4));

        Assert.Equal(3, quote.Nights);
        Assert.Equal(3000m, quote.Subtotal);
        Assert.Equal(360m, quote.ServiceFee);
        Assert.Equal(200m, quote.CleaningFee);
        Assert.Equal(3560m, quote.Total);
    }

    [Fact]
    public void Service_fee_applies_to_the_nights_only_not_the_cleaning_fee()
    {
        var withCleaning = Calculator().Quote(1000m, 500m, new DateTime(2026, 12, 1), new DateTime(2026, 12, 2));
        var withoutCleaning = Calculator().Quote(1000m, 0m, new DateTime(2026, 12, 1), new DateTime(2026, 12, 2));

        Assert.Equal(withoutCleaning.ServiceFee, withCleaning.ServiceFee);
    }

    [Fact]
    public void Service_fee_is_rounded_to_cents()
    {
        // 12% of R999.99 = R119.9988, which is R120.00 to the cent
        var quote = Calculator().Quote(999.99m, 0m, new DateTime(2026, 12, 1), new DateTime(2026, 12, 2));

        Assert.Equal(120.00m, quote.ServiceFee);
        Assert.Equal(1119.99m, quote.Total);
    }

    [Fact]
    public void Service_fee_rate_comes_from_configuration()
    {
        var quote = Calculator(0.05m).Quote(1000m, 0m, new DateTime(2026, 12, 1), new DateTime(2026, 12, 3));

        Assert.Equal(100m, quote.ServiceFee);
        Assert.Equal(2100m, quote.Total);
    }

    [Fact]
    public void Times_of_day_do_not_change_the_number_of_nights()
    {
        var quote = Calculator().Quote(1000m, 0m, new DateTime(2026, 12, 1, 23, 30, 0), new DateTime(2026, 12, 3, 1, 0, 0));

        Assert.Equal(2, quote.Nights);
    }
}
