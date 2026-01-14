using AlienCyborgModernTempleOS;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 200_000_000; // 200 MB
});

builder.Services.AddHttpClient<LmStudioChatClient>(client =>
{
    client.BaseAddress = new Uri("http://localhost:1234/v1/");
});

builder.Services.AddSingleton(sp =>
{
    var llm = sp.GetRequiredService<LmStudioChatClient>();
    var model = builder.Configuration["LmStudio:Model"] ?? "zai-org/glm-4.6v-flash";
    return new AlienOrchestrator(llm, model);
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 200_000_000; // 200 MB
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
