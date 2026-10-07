using LoanPaymentCalculator;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 32_768);
var app = builder.Build();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; frame-ancestors 'none'";
    await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapPost("/api/schedule", (LoanRequest request) =>
{
    try { return Results.Ok(ScheduleCalculator.Calculate(request)); }
    catch (Exception exception) when (exception is ArgumentException or OverflowException)
    { return Results.BadRequest(new { error = exception is OverflowException ? "Bu değerler hesaplama sınırını aşıyor; vade veya oranı azaltın." : exception.Message }); }
});
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.Run();
