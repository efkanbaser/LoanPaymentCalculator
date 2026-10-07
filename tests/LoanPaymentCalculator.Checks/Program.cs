using LoanPaymentCalculator;

var basic = new LoanRequest(1000m, 12m, 1, "Ay", 1, 0, 0, 0, 0, new(2026, 1, 1), false);
int passed = 0;
void Equal<T>(T expected, T actual) where T : IEquatable<T>
{ if (!expected.Equals(actual)) throw new Exception($"Expected {expected}; got {actual}."); }
void Reject(LoanRequest input)
{ try { ScheduleCalculator.Calculate(input); } catch (ArgumentException) { return; } throw new Exception("Invalid input was accepted."); }
void Check(string name, Action action) { action(); passed++; Console.WriteLine($"PASS {name}"); }

Check("single-payment hand-calculated reference", () => {
    var plan = ScheduleCalculator.Calculate(basic);
    Equal(1010m, plan.TotalPayment); Equal(1000m, plan.TotalPrincipal); Equal(10m, plan.TotalInterest); Equal(0m, plan.Rows[0].Balance);
});
Check("tax component hand-calculated reference", () => {
    var plan = ScheduleCalculator.Calculate(basic with { Kkdf = 15m, Bsmv = 15m });
    Equal(1013m, plan.TotalPayment); Equal(10m, plan.TotalInterest); Equal(3m, plan.TotalTaxComponent);
});
Check("zero rate and final-cent adjustment", () => {
    var plan = ScheduleCalculator.Calculate(basic with { AnnualRate = 0, Term = 3 });
    Equal(333.33m, plan.FirstPayment); Equal(333.34m, plan.LastPayment); Equal(1000m, plan.TotalPrincipal);
});
Check("three-month effective compounding", () => {
    var plan = ScheduleCalculator.Calculate(basic with { Term = 3, PaymentInterval = 3 });
    Equal(1030.30m, plan.TotalPayment); Equal(30.30m, plan.TotalInterest);
});
Check("interest-only phase retains principal", () => {
    var plan = ScheduleCalculator.Calculate(basic with { Term = 4, PrincipalGrace = 2 });
    Equal(0m, plan.Rows[0].Principal); Equal(10m, plan.FirstPayment); Equal(1000m, plan.Rows[1].Balance); Equal(1000m, plan.TotalPrincipal);
});
Check("source-specific full grace accrual including initial step", () => {
    var plan = ScheduleCalculator.Calculate(basic with { Term = 3, PrincipalGrace = 2, InterestGrace = 2 });
    Equal(1, plan.Rows.Length); Equal(1030.30m, plan.TotalPayment); Equal(1000m, plan.TotalPrincipal);
});
Check("month-end advances from previous payment date", () => {
    var plan = ScheduleCalculator.Calculate(basic with { Term = 2, StartDate = new(2028, 1, 31) });
    Equal(new DateOnly(2028, 2, 29), plan.Rows[0].Date); Equal(new DateOnly(2028, 3, 29), plan.Rows[1].Date);
});
Check("weekend plus caller-supplied holiday shift", () => {
    var plan = ScheduleCalculator.Calculate(basic with { Period = "Gün", StartDate = new(2026, 10, 9), ShiftBusinessDays = true, Holidays = [new(2026, 10, 12)] });
    Equal(new DateOnly(2026, 10, 13), plan.Rows[0].Date);
});
Check("date shift does not recalculate interest", () => {
    var input = basic with { Period = "Gün", StartDate = new(2026, 10, 9) };
    Equal(ScheduleCalculator.Calculate(input).TotalPayment, ScheduleCalculator.Calculate(input with { ShiftBusinessDays = true }).TotalPayment);
});
Check("invalid amount and zero interval rejected", () => { Reject(basic with { Principal = 0 }); Reject(basic with { PaymentInterval = 0 }); });
Check("invalid grace and period alignment rejected", () => { Reject(basic with { PrincipalGrace = 1 }); Reject(basic with { Term = 12, PaymentInterval = 3, PrincipalGrace = 3, InterestGrace = 1 }); });
Check("yearly nominal conversion reference", () => {
    var plan = ScheduleCalculator.Calculate(basic with { Period = "Yıl" }); Equal(1120m, plan.TotalPayment);
});
Console.WriteLine($"{passed} calculation checks passed.");
