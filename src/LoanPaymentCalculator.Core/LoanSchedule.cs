using System.Globalization;

namespace LoanPaymentCalculator;

public sealed record LoanRequest(
    decimal Principal, decimal AnnualRate, int Term, string Period, int PaymentInterval,
    int PrincipalGrace, int InterestGrace, decimal Kkdf, decimal Bsmv,
    DateOnly StartDate, bool ShiftBusinessDays, DateOnly[]? Holidays = null);

public sealed record PaymentRow(int Number, DateOnly Date, decimal Payment, decimal Principal,
    decimal Interest, decimal Balance)
{
    // Derived from the original six-column output; not a separate tax calculation.
    public decimal TaxComponent => Payment - Principal - Interest;
}

public sealed record LoanSchedule(LoanRequest Inputs, PaymentRow[] Rows, string OriginalTsv)
{
    public decimal TotalPayment => Rows.Sum(row => row.Payment);
    public decimal TotalPrincipal => Rows.Sum(row => row.Principal);
    public decimal TotalInterest => Rows.Sum(row => row.Interest);
    public decimal TotalTaxComponent => Rows.Sum(row => row.TaxComponent);
    public decimal FirstPayment => Rows[0].Payment;
    public decimal LastPayment => Rows[^1].Payment;
}

public static class ScheduleCalculator
{
    public static LoanSchedule Calculate(LoanRequest input)
    {
        Validate(input);
        var tsv = new OriginalCalculator(input.Holidays).ItfaPlanHesapla(
            input.StartDate.ToDateTime(TimeOnly.MinValue), input.Principal, input.Term,
            input.PrincipalGrace, input.PaymentInterval, input.Period, "Yıllık",
            input.AnnualRate, input.Kkdf, input.Bsmv, input.InterestGrace, ".", input.ShiftBusinessDays);
        if (!tsv.Contains('\t')) throw new ArgumentException(tsv);
        var culture = CultureInfo.GetCultureInfo("en-GB");
        var rows = tsv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line =>
        {
            var fields = line.TrimEnd('\r').Split('\t');
            return new PaymentRow(int.Parse(fields[1], culture),
                DateOnly.ParseExact(fields[0], "dd.MM.yyyy", CultureInfo.InvariantCulture),
                decimal.Parse(fields[2], culture), decimal.Parse(fields[3], culture),
                decimal.Parse(fields[4], culture), decimal.Parse(fields[5], culture));
        }).ToArray();
        return new(input, rows, tsv);
    }

    private static void Validate(LoanRequest input)
    {
        if (input.Principal <= 0 || input.Principal > 1_000_000_000m)
            throw new ArgumentException("Kredi tutarı 0'dan büyük, en fazla 1 milyar olmalı.");
        if (input.AnnualRate < 0 || input.AnnualRate > 200 || input.Kkdf < 0 || input.Kkdf > 100 || input.Bsmv < 0 || input.Bsmv > 100)
            throw new ArgumentException("Faiz %0–200; vergi oranları %0–100 aralığında olmalı.");
        if (input.Term is < 1 or > 600 || input.PaymentInterval < 1 || input.PaymentInterval > input.Term)
            throw new ArgumentException("Vade 1–600, ödeme aralığı 1–vade aralığında olmalı.");
        if (input.Period is not ("Gün" or "Ay" or "Yıl"))
            throw new ArgumentException("Vade birimi gün, ay veya yıl olmalı.");
        if (input.PrincipalGrace < 0 || input.InterestGrace < 0 || input.PrincipalGrace >= input.Term || input.InterestGrace > input.PrincipalGrace)
            throw new ArgumentException("Faiz ödemesiz süre ≤ anapara ödemesiz süre < vade olmalı.");
        if ((input.Term - input.PrincipalGrace) % input.PaymentInterval != 0 ||
            (input.PrincipalGrace - input.InterestGrace) % input.PaymentInterval != 0 || input.InterestGrace % input.PaymentInterval != 0)
            throw new ArgumentException("Vade ve ödemesiz süreler ödeme aralığına tam bölünmeli.");
        if (input.StartDate == default || input.Holidays?.Length > 366)
            throw new ArgumentException("Başlangıç tarihi gerekli; en fazla 366 özel tatil tarihi girilebilir.");
    }
}
