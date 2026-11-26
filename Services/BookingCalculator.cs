using Microsoft.Extensions.Options;

namespace Staybnb.Services;

public class BookingOptions
{
    public decimal ServiceFeeRate { get; set; } = 0.12m;
}

public record BookingQuote(int Nights, decimal PricePerNight, decimal Subtotal, decimal CleaningFee, decimal ServiceFee, decimal Total);

public class BookingCalculator(IOptions<BookingOptions> options)
{
    public decimal ServiceFeeRate => options.Value.ServiceFeeRate;

    // TotalPrice = (PricePerNight x Nights) + CleaningFee + ServiceFee
    public BookingQuote Quote(decimal pricePerNight, decimal cleaningFee, DateTime checkIn, DateTime checkOut)
    {
        var nights = (checkOut.Date - checkIn.Date).Days;
        var subtotal = pricePerNight * nights;
        var serviceFee = Math.Round(subtotal * ServiceFeeRate, 2);
        return new BookingQuote(nights, pricePerNight, subtotal, cleaningFee, serviceFee, subtotal + cleaningFee + serviceFee);
    }
}
