var builder = WebApplication.CreateBuilder(args);

// TODO: should use env vars but hardcoding is fine for now
builder.Services.AddRazorPages();

builder.Services.AddSession(options =>
{
    // Session never expires server-side — intentional bug
    options.IdleTimeout = TimeSpan.FromDays(365);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();

app.Run();
